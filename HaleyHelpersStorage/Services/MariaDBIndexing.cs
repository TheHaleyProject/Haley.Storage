using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using static Haley.Internal.IndexingConstant;
using static Haley.Internal.IndexingQueries;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Haley.Utils {
    public class MariaDBIndexing : IDSSIndexing {
        const string DB_CORE_SQL_FILE = "dsscore.sql";
        const string DB_CLIENT_SQL_FILE = "dssclient.sql";
        const string DB_CORE_FALLBACK_NAME = "mss_core";
        const string DB_CORE_SEARCH_TERM = "dss_core";
        const string DB_CLIENT_SEARCH_TERM = "dss_client";
        const string DB_SQL_FILE_LOCATION = "Resources";
        public const string DB_MODULE_NAME_PREFIX = "dssm_";
        ILogger _logger;
        string _key;
        IAdapterGateway _agw;
        bool isValidated = false;
        async Task EnsureValidation() {
            if (!isValidated) await Validate();
        }

        ConcurrentDictionary<string, IOSSDirectory> _cache = new ConcurrentDictionary<string, IOSSDirectory>();
        public bool ThrowExceptions { get; set; }
        public (long id, Guid guid) UIDManager(IOSSRead request, IOSSControlled holder) {
            return UIDManagerInternal(request,holder).Result;
        }
        async Task<(bool status, long id)> EnsureWorkSpace(IOSSRead request) {
            if (!_cache.ContainsKey(request.Workspace.Cuid)) return (false, 0);
            var dbid = request.Module.Cuid;
            var wspace = _cache[request.Workspace.Cuid];
            //Check if workspace exists in the database.
            var ws = await InsertAndFetchIDScalar(dbid,
                () => (INSTANCE.WORKSPACE.EXISTS, Consolidate((ID, wspace.Id))),
                () => (INSTANCE.WORKSPACE.INSERT, Consolidate((ID, wspace.Id))),
                $@"Unable to insert the workspace  {wspace.Id}");
            return (true, wspace.Id);
        }

        async Task<(bool status, (long id, string uid) result)> EnsureDirectory(IOSSRead request, long ws_id) {
            if (ws_id == 0) return (false, (0, string.Empty));
            var dbid = request.Module.Cuid;
            //If directory name is not provided, then go for "default" as usual
            var dirParent = request.Folder?.Parent?.Id ?? 0;
            var dirName = request.Folder?.Name ?? OSSInfo.DEFAULTNAME;
            var dirDbName = dirName.ToDBName();

            var dirInfo = await InsertAndFetchIDRead(dbid, 
                () => (INSTANCE.DIRECTORY.EXISTS, Consolidate((WSPACE, ws_id), (PARENT, dirParent), (NAME, dirDbName))), 
                () => (INSTANCE.DIRECTORY.INSERT, Consolidate((WSPACE, ws_id), (PARENT, dirParent), (NAME, dirDbName), (DNAME, dirName))), 
                $@"Unable to insert the directory {dirName} to the workspace : {ws_id}");
           
            return (true, (dirInfo.id, dirInfo.uid));
        }

        async Task<long> InsertAndFetchIDScalar(string dbid, Func<(string query,(string key,object value)[] parameters)> check, Func<(string query, (string key, object value)[] parameters)> insert = null, string failureMessage = "Error", bool preCheck = true) {
            if (check == null) return 0;
           
            var checkInput = check.Invoke();
            object info = null;
            if (preCheck) info = await _agw.Scalar(new AdapterArgs(dbid) { Query = checkInput.query }, checkInput.parameters);

            if (info == null) {
                if (insert == null) return 0;
                var insertInput = insert.Invoke();
                await _agw.NonQuery(new AdapterArgs(dbid) { Query = insertInput.query }, insertInput.parameters);
                info = await _agw.Scalar(new AdapterArgs(dbid) { Query = checkInput.query }, checkInput.parameters);
            }
            long id = 0;
            if (info == null || !long.TryParse(info.ToString(), out id)) throw new Exception($@"{failureMessage} from the database {dbid}");
            return id;
        }

        async Task<(long id, string uid)> InsertAndFetchIDRead(string dbid, Func<(string query, (string key, object value)[] parameters)> check = null, Func<(string query, (string key, object value)[] parameters)> insert = null, string failureMessage = "Error", bool preCheck = true) {
            if (check == null) return (0, string.Empty);
            var checkInput = check.Invoke();

            object info = null;
            if (preCheck) info = await _agw.Read(new AdapterArgs(dbid) { Query = checkInput.query, Filter = ResultFilter.FirstDictionary }, checkInput.parameters);

            if (info == null || !(info is Dictionary<string, object> dic1) || dic1.Count < 1) {
                if (insert == null) return (0, string.Empty);
                var insertInput = insert.Invoke();
                await _agw.NonQuery(new AdapterArgs(dbid) { Query = insertInput.query }, insertInput.parameters);
                info = await _agw.Read(new AdapterArgs(dbid) { Query = checkInput.query, Filter = ResultFilter.FirstDictionary }, checkInput.parameters);
            }
            long id = 0;
            if (info == null || !(info is Dictionary<string, object> dic) || dic.Count < 1) throw new Exception($@"{failureMessage} from the database {dbid}");
            return ((long)dic["id"], (string)dic["uid"]);
        }

        (string key,object value)[] Consolidate(params (string, object)[] parameters) {
            return parameters;
        }

       async Task<(bool status, long id)> EnsureNameStore(IOSSRead request) {
            if (string.IsNullOrWhiteSpace(request.TargetName)) return (false, 0);
            var name = Path.GetFileNameWithoutExtension(request.TargetName)?.Trim();
            var ext = Path.GetExtension(request.TargetName)?.Trim();
            if (string.IsNullOrWhiteSpace(ext)) ext = OSSInfo.DEFAULTNAME;
            if (string.IsNullOrWhiteSpace(name)) return (false, 0);
            name = name.ToDBName();
            ext = ext.ToDBName();

            var dbid = request.Module.Cuid;

            //Extension Exists?
            long extId = await InsertAndFetchIDScalar(dbid, () => (INSTANCE.EXTENSION.EXISTS, Consolidate((NAME, ext))), () => (INSTANCE.EXTENSION.INSERT,Consolidate((NAME, ext))), $@"Unable to fetch extension id for {ext}");

            // Name Exists ?
            long nameId = await InsertAndFetchIDScalar(dbid, () => (INSTANCE.VAULT.EXISTS, Consolidate((NAME, name))), () => (INSTANCE.VAULT.INSERT, Consolidate((NAME, name))), $@"Unable to fetch name id for {name}");

            //Namestore Exists?
            long nsId = await InsertAndFetchIDScalar(dbid, () => (INSTANCE.NAMESTORE.EXISTS, Consolidate((NAME, nameId), (EXT, extId))), () => (INSTANCE.NAMESTORE.INSERT, Consolidate((NAME, nameId), (EXT, extId))), $@"Unable to fetch name store id for name : {name} and extension : {ext}");

            return (true, nsId);
        }

        async Task<(long id,Guid guid)> UIDManagerInternal(IOSSRead request, IOSSControlled holder) {
            try {
                //If we are in ParseMode, we still do all the process, but, store the file as is with Parsing information.
                //For parse mode, let us not throw any exception.
                (long id, Guid guid) result = (0, Guid.Empty);
                var ws = await EnsureWorkSpace(request);
                if (!ws.status) return result;
                var dir = await EnsureDirectory(request, ws.id);
                if (!dir.status) return result;
                var ns = await EnsureNameStore(request);
                if (!ns.status) return result;

                var dbid = request.Module.Cuid;
                var docInfo = await InsertAndFetchIDRead(dbid,() => (INSTANCE.DOCUMENT.EXISTS, Consolidate((PARENT, dir.result.id), (NAME, ns.id))));
                bool docExists = docInfo.id != 0;
                if (!docExists) {
                    // Insert it.
                    docInfo = await InsertAndFetchIDRead(dbid, 
                        () => (INSTANCE.DOCUMENT.EXISTS, Consolidate((PARENT, dir.result.id), (NAME, ns.id))),
                        ()=> (INSTANCE.DOCUMENT.INSERT, Consolidate((WSPACE,ws.id), (PARENT, dir.result.id), (NAME, ns.id))),
                        $@"Unable to insert document with name {request.TargetName}",false);
                    var dname = Path.GetFileName(request.TargetName);
                    await _agw.NonQuery(new AdapterArgs(dbid) { Query = INSTANCE.DOCUMENT.INSERT_INFO }, (PARENT, docInfo.id), (DNAME, dname));
                }

                int version = 1;
                //If Doc exists.. we just need to revise the version.
                if (docExists) {
                    //Assuming that there is a version. Get the latest version.
                    var currentVersion = await _agw.Scalar(new AdapterArgs(dbid) { Query = INSTANCE.DOCVERSION.FIND_LATEST }, (PARENT, docInfo.id));
                    if (currentVersion != null && int.TryParse(currentVersion.ToString(),out int cver)) {
                        version = ++cver;
                    }
                }

                var dvInfo = await InsertAndFetchIDRead(dbid,
                    () => (INSTANCE.DOCVERSION.EXISTS, Consolidate((PARENT, docInfo.id), (VERSION, version))),
                    () => (INSTANCE.DOCVERSION.INSERT, Consolidate((PARENT, docInfo.id), (VERSION, version))),
                    $@"Unable to insert document version for the document {docInfo.id}", false);

                if (dvInfo.id > 0 && !string.IsNullOrWhiteSpace(dvInfo.uid)) {
                    //Check if the incoming uid is in proper GUID format.
                    Guid dvId = Guid.Empty;
                    if (dvInfo.uid.IsValidGuid(out dvId) || dvInfo.uid.IsCompactGuid(out dvId)) {
                        result = (dvInfo.id, dvId);
                    }
                }
                
                if (holder != null) {
                    holder.ForceSetCuid(result.guid);
                    holder.ForceSetId(result.id);
                    holder.Version = version;
                }

                return result;
            } catch (Exception ex) {
                _logger?.LogError(ex.Message);
                if (ThrowExceptions) throw ex; //For Parse mode, let us not throw any exceptions.
                return (0, Guid.Empty);
            }
        }
        public async Task<IFeedback> RegisterClient(IOSSClient info) {
            if (info == null) throw new ArgumentNullException("Input client directory info cannot be null");
            if (!info.TryValidate(out var msg)) throw new ArgumentException(msg);
            //We generate the hash_guid ourselves for the client.
            await EnsureValidation();

            //Do we even need to check if the client exists? Why dont' we directly upsert the values??? We need to check, because, if we try upsert, then each time , we end up with a new autogenerated id that is not consumed. So, we might end up with all ids' consumed in years. For safer side, we use upsert, also, we check if id exists and try to update separately.

            var exists = await _agw.Scalar(new AdapterArgs(_key) { Query = CLIENT.EXISTS }, (NAME, info.Name));
            var thandler = _agw.GetTransactionHandler(_key); //For both cases, update or upsert, we use inside a transaction.
            if (exists != null && exists is int cliId) {
                //Client exists. We just need to update.
                using (thandler.Begin()) {
                    //Register client
                    await _agw.NonQuery((new AdapterArgs(_key) { Query = CLIENT.UPDATE }).ForTransaction(thandler), (DNAME, info.DisplayName), (PATH, info.Path),(ID, cliId));
                    await _agw.NonQuery((new AdapterArgs(_key) { Query = CLIENT.UPSERTKEYS }).ForTransaction(thandler), (ID, cliId), (SIGNKEY, info.SigningKey), (ENCRYPTKEY, info.EncryptKey), (PASSWORD, info.PasswordHash));
                }
            } else {
                
                using (thandler.Begin()) {
                    //Register client
                    await _agw.NonQuery((new AdapterArgs(_key) { Query = CLIENT.UPSERT }).ForTransaction(thandler), (NAME, info.Name), (DNAME, info.DisplayName), (GUID, info.Guid), (PATH, info.Path));
                    exists = await _agw.Scalar((new AdapterArgs(_key) { Query = CLIENT.EXISTS }).ForTransaction(thandler), (NAME, info.Name));
                    if (exists != null && exists is int clientId) {
                        //await _agw.Read(new AdapterArgs(_key) { Query = $@"select * from client as c where c.id = {clientId};" });
                        //Add Info
                        await _agw.NonQuery((new AdapterArgs(_key) { Query = CLIENT.UPSERTKEYS }).ForTransaction(thandler), (ID, clientId), (SIGNKEY, info.SigningKey), (ENCRYPTKEY, info.EncryptKey), (PASSWORD, info.PasswordHash));
                    }
                }
            }
            return await ValidateAndCache(CLIENT.EXISTS, "Client", info, null, (NAME, info.Name));
        }
        public async Task<IFeedback> RegisterModule(IOSSModule info) {
            if (info == null) throw new ArgumentNullException("Input Module directory info cannot be null");
            if (!info.TryValidate(out var msg)) throw new ArgumentNullException(msg);
            //We generate the hash_guid ourselves for the client.
            await EnsureValidation();

            //Check if client exists. If not throw exeception or don't register? //Send feedback.
            //var cexists = await _agw.Scalar(new AdapterArgs(_key) { Query = CLIENT.EXISTS }, (NAME, info.Client.Name));
            //if (cexists == null || !(cexists is int clientId)) throw new ArgumentException($@"Client {info.Client.Name} doesn't exist. Unable to index the module {info.DisplayName}.");
            //var mexists = await _agw.Scalar(new AdapterArgs(_key) { Query = MODULE.EXISTS }, (NAME, info.Name), (PARENT, clientId));
            var exists = await _agw.Scalar(new AdapterArgs(_key) { Query = MODULE.EXISTS_BY_CUID }, (CUID,info.Cuid));
            if (exists != null && exists is long mId) {
                //Module exists. .just update it.
                await _agw.NonQuery(new AdapterArgs(_key) { Query = MODULE.UPDATE }, (DNAME, info.DisplayName), (PATH, info.Path), (ID, mId));
            } else {
                var cexists = await _agw.Scalar(new AdapterArgs(_key) { Query = CLIENT.EXISTS }, (NAME, info.Client.Name));
                if (cexists == null || !(cexists is int clientId)) throw new ArgumentException($@"Client {info.Client.Name} doesn't exist. Unable to index the module {info.DisplayName}.");
                await _agw.NonQuery(new AdapterArgs(_key) { Query = MODULE.UPSERT }, (PARENT, clientId), (NAME, info.Name), (DNAME, info.DisplayName), (GUID, info.Guid), (PATH, info.Path), (CUID, info.Cuid));
            }
            return await ValidateAndCache(MODULE.EXISTS_BY_CUID, "Module", info, CreateModuleDBInstance, (CUID, info.Cuid));
        }
        public async Task<IFeedback> RegisterWorkspace(IOSSWorkspace info) {
            if (info == null) throw new ArgumentNullException("Input Module directory info cannot be null");
            if (!info.TryValidate(out var msg)) throw new ArgumentNullException(msg);
            //We generate the hash_guid ourselves for the client.
            await EnsureValidation();

            var exists = await _agw.Scalar(new AdapterArgs(_key) { Query = WORKSPACE.EXISTS_BY_CUID }, (CUID, info.Cuid));
            if (exists != null && exists is long wsId) {
                //Module exists. .just update it.
                await _agw.NonQuery(new AdapterArgs(_key) { Query = WORKSPACE.UPDATE }, (DNAME, info.DisplayName), (PATH, info.Path), (CONTROLMODE, (int)info.ControlMode), (PARSEMODE, (int)info.ParseMode),(ID, wsId));
            } else {
                var moduleCuid = OSSUtils.GenerateCuid(info.Client.Name, info.Module.Name);
                var mexists = await _agw.Scalar(new AdapterArgs(_key) { Query = MODULE.EXISTS_BY_CUID }, (CUID, moduleCuid));
                if (mexists == null || !(mexists is int modId)) throw new ArgumentException($@"Module {info.Module.Name} doesn't exist. Unable to index the module {info.DisplayName}.");
                await _agw.NonQuery(new AdapterArgs(_key) { Query = WORKSPACE.UPSERT }, (PARENT, modId), (NAME, info.Name), (DNAME, info.DisplayName), (GUID, info.Guid), (PATH, info.Path), (CUID, info.Cuid), (CONTROLMODE, (int)info.ControlMode), (PARSEMODE, (int)info.ParseMode));
            }
            return await ValidateAndCache(WORKSPACE.EXISTS_BY_CUID, "Workspace", info, null, (CUID, info.Cuid));
        }
        async Task<IFeedback> ValidateAndCache(string query, string title, IOSSDirectory info, Func<IOSSDirectory, Task> preProcess, params (string key, object value)[] parameters) {
            var result = await _agw.Scalar(new AdapterArgs(_key) { Query = query }, parameters);
            if (result != null && result.IsNumericType()) {
                if (long.TryParse(result.ToString(), out var id)) info.ForceSetId(id);
                //Every time a client is sucessfully done. We validate if it is present or not.
                await AddComponentCache(info, preProcess);
                return new Feedback(true, $@"{title} - {info.Name} Indexed.") { Result = id };
            }
            return new Feedback(false, "Unable to index");
        }
        public async Task Validate() {
            try {
                //If the service or the db doesn't exist, we throw exception or else the system would assume that nothing is wrong. If they wish , they can still turn of the indexing.
                if (!_agw.ContainsKey(_key)) throw new ArgumentException($@"Storage Indexing service validation failure.No adapter found for the given key {_key}");
                //Next step is to find out if the database exists or not? Should we even try to check if the database exists or directly run the sql script and create the database if it doesn't exists?
                var dbname = _agw[_key].Info?.DBName ?? DB_CORE_FALLBACK_NAME; //This is supposedly our db name.
                var exists = await _agw.Scalar(new AdapterArgs(_key) { ExcludeDBInConString = true, Query = GENERAL.SCHEMA_EXISTS }, (NAME, dbname));
                if (exists != null && exists.IsNumericType()) return;
                var sqlFile = Path.Combine(AssemblyUtils.GetBaseDirectory(), DB_SQL_FILE_LOCATION, DB_CORE_SQL_FILE);
                if (!File.Exists(sqlFile)) throw new ArgumentException($@"Master sql file for creating the storage DB is not found. Please check : {DB_CORE_SQL_FILE}");
                //if the file exists, then run this file against the adapter gateway but ignore the db name.
                var content = File.ReadAllText(sqlFile);
                //We know that the file itself contains "dss_core" as the schema name. Replace that with new one.
               
                content = content.Replace(DB_CORE_SEARCH_TERM, dbname);
                //?? Should we run everything in one go or run as separate statements???
                //if the input contains any delimiter or procedure, remove them.
                object queryContent = content;
                List<string> procedures = new();
                if (content.Contains("Delimiter", StringComparison.InvariantCultureIgnoreCase)) {
                    //Step 1 : Remove delimiter lines
                    content = Regex.Replace(content, @"DELIMITER\s+\S+", "", RegexOptions.IgnoreCase); //Remove the delimiter comments
                                                                                                       //Step 2 : Remove version-specific comments
                    content = Regex.Replace(content, @"/\*!.*?\*/;", "", RegexOptions.Singleline);
                    //Step 3 : Extract all Procedures
                    string pattern = @"CREATE\s+PROCEDURE.*?END\s*//";
                    var matches = Regex.Matches(content, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    foreach (Match match in matches) {
                        string proc = match.Value;
                        proc = proc.Replace("//", ";").Trim();
                        procedures.Add(proc);
                        content = content.Replace(match.Value, "");
                    }
                    // Step 4: Split remaining SQL by semicolon
                    queryContent = Regex.Split(content, @";\s*(?=\n|$)", RegexOptions.Multiline);
                    //queryContent = Regex.Split(content, @";\s*(?=\n|$)", RegexOptions.Multiline);
                }

                var handler = _agw.GetTransactionHandler(_key);
                using (handler.Begin(true)) {
                    await _agw.NonQuery(new AdapterArgs(_key) { ExcludeDBInConString = true, Query = queryContent }.ForTransaction(handler));
                    if (procedures.Count > 0) {
                        await _agw.NonQuery(new AdapterArgs(_key) { ExcludeDBInConString = true, Query = procedures.ToArray() }.ForTransaction(handler));
                    }
                }
                isValidated = true;
            } catch (Exception ex) {
                throw ex;
            }
           
        }
        async Task AddComponentCache(IOSSDirectory info, Func<IOSSDirectory,Task> preProcess = null) {
            if (info == null) return;
            if (_cache.ContainsKey(info.Cuid) && _cache[info.Cuid] != null) return; 
            
            if (preProcess != null) {
                await preProcess(info);
            }   

            if (_cache.ContainsKey(info.Cuid)) {
                _cache.TryUpdate(info.Cuid, info, null); //Gives the schema name
            } else {
                _cache.TryAdd(info.Cuid, info);
            }
        }
        async Task CreateModuleDBInstance(IOSSDirectory dirInfo) {
            if (!(dirInfo is IOSSModule info)) return;
            if (string.IsNullOrWhiteSpace(info.DatabaseName)) info.DatabaseName = $@"{DB_MODULE_NAME_PREFIX}{info.Cuid}";
            //What if the CUID is changed? Should we use the guid instead? 
            //But, guid is not unique across clients. So, we use cuid.
            //So, when we create the module, we use the cuid as the database name.
            //TODO : IF A CUID IS CHANGED, THEN WE NEED TO UPDATE THE DATABASE NAME IN THE DB.
            var sqlFile = Path.Combine(AssemblyUtils.GetBaseDirectory(), DB_SQL_FILE_LOCATION, DB_CLIENT_SQL_FILE);
            if (!File.Exists(sqlFile)) throw new ArgumentException($@"Master sql for client file is not found. Please check : {DB_CLIENT_SQL_FILE}");
            //if the file exists, then run this file against the adapter gateway but ignore the db name.
            var content = File.ReadAllText(sqlFile);
            //We know that the file itself contains "dss_core" as the schema name. Replace that with new one.
            var exists = await _agw.Scalar(new AdapterArgs(_key) { ExcludeDBInConString = true, Query = GENERAL.SCHEMA_EXISTS }, (NAME, info.DatabaseName));
            if (exists == null || !exists.IsNumericType() || !double.TryParse(exists.ToString(),out var id) || id < 1) {
                content = content.Replace(DB_CLIENT_SEARCH_TERM, info.DatabaseName);
                //?? Should we run everything in one go or run as separate statements ???
                var result = await _agw.NonQuery(new AdapterArgs(_key) { ExcludeDBInConString = true, Query = content });
            }
            exists = await _agw.Scalar(new AdapterArgs(_key) { ExcludeDBInConString = true, Query = GENERAL.SCHEMA_EXISTS }, (NAME, info.DatabaseName));
            if (exists == null) throw new ArgumentException($@"Unable to generate the database {info.DatabaseName}");
            //We create an adapter with this Cuid and store them.
            _agw.DuplicateAdapter(_key, info.Cuid, ("database",info.DatabaseName));
            
        }
        public bool TryAddInfo(IOSSDirectory dirInfo, bool replace = false) {
            if (dirInfo == null || !dirInfo.Name.AssertValue(false) || !dirInfo.Cuid.AssertValue(false)) return false;
            if (_cache.ContainsKey(dirInfo.Cuid)) {
                if (!replace) return false;
                return _cache.TryUpdate(dirInfo.Cuid, dirInfo, _cache[dirInfo.Cuid]);
            } else {
                return _cache.TryAdd(dirInfo.Cuid, dirInfo);
            }
            return true;
        }
        public bool TryGetComponentInfo<T>(string key, out T component) where T : IOSSDirectory {
            component = default(T);
            if (string.IsNullOrWhiteSpace(key) || !_cache.ContainsKey(key)) return false;
            var data = _cache[key];
            if (data == null || !(data is T)) return false;
            component = (T)data;
            return true;
        }
        public MariaDBIndexing(IAdapterGateway agw, string key, ILogger logger) : this(agw, key, logger, false) { }
        public MariaDBIndexing(IAdapterGateway agw, string key, ILogger logger, bool throwExceptions) {
            _key = key;
            _agw = agw;
            _logger = logger;
            ThrowExceptions = throwExceptions;
        }
    }
}
