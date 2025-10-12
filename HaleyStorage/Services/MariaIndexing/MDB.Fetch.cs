using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ObjectiveC;
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
    public partial class MariaDBIndexing : IDSSIndexing {
        public Task<IFeedback> GetDocVersionInfo(string moduleCuid, long id) {
            return GetDocVersionInfoInternal(moduleCuid, id, string.Empty);
        }
        public Task<IFeedback> GetDocVersionInfo(string moduleCuid, string cuid) {
            return GetDocVersionInfoInternal(moduleCuid, 0, cuid);
        }
        public async Task<IFeedback> GetDocVersionInfo(string moduleCuid, long wsId, string file_name, string dir_name = OSSConstants.DEFAULT_NAME, long dir_parent_id = 0) {
            Feedback result = new Feedback();
            try {
                if (string.IsNullOrWhiteSpace(moduleCuid) || wsId < 1) return result.SetMessage($@"Module CUID & non-zero worspace id are mandatory to fetch document info");
                if (string.IsNullOrWhiteSpace(file_name)) return result.SetMessage($@"For this approach, file name required for searching.");
                if (!_agw.ContainsKey(moduleCuid)) return result.SetMessage($@"No adapter found for the key {moduleCuid}");
                
                var name = Path.GetFileNameWithoutExtension(file_name).ToDBName();
                var extension = Path.GetExtension(file_name)?.ToDBName() ?? OSSConstants.DEFAULT_NAME;

                var docInfo = await _agw.Scalar(new AdapterArgs(moduleCuid) { Query = INSTANCE.DOCUMENT.GET_BY_NAME }, (NAME,name),(EXT, extension),(WSPACE,wsId),(PARENT,dir_parent_id),(DIRNAME,dir_name.ToDBName()));
                if (docInfo == null || !long.TryParse(docInfo.ToString(),out var docId)) return result.SetMessage($@"Unable to fetch the document for the given inputs. FileName :  {file_name} ; WSID : {wsId} ; DirName : {dir_name}");

                var data = await _agw.Read(new AdapterArgs(moduleCuid) { Query = INSTANCE.DOCVERSION.GET_LATEST_BY_PARENT, Filter = ResultFilter.FirstDictionary }, (PARENT,docId));
                if (data == null || !(data is Dictionary<string, object> dic) || dic.Count < 1) return result.SetMessage($@"Unable to fetch the document version info for the given inputs. Document Id : {docId} ; FileName :  {file_name} ; WSID : {wsId} ; DirName : {dir_name}");
                return result.SetStatus(true).SetMessage("Document version info obtained").SetResult(dic);
            } catch (Exception ex) {
                _logger?.LogError(ex.StackTrace);
                return result.SetMessage(ex.StackTrace);
            }

        }

        public async Task<IFeedback> GetDocVersionInfo(string moduleCuid, string wsCuid, string file_name, string dir_name = OSSConstants.DEFAULT_NAME, long dir_parent_id = 0) {
            try {
                if (string.IsNullOrWhiteSpace(wsCuid)) return new Feedback() { Message = "Workspace CUID cannot be empty." };
                var wsInfo = await _agw.Scalar(new AdapterArgs(_key) { Query = WORKSPACE.EXISTS_BY_CUID }, (CUID, wsCuid));
                if(wsInfo != null && long.TryParse(wsInfo.ToString(),out var wsId)) {
                    return await GetDocVersionInfo(moduleCuid,wsId,file_name,dir_name,dir_parent_id);
                }
                return new Feedback() { Message = "Unable to fetch the information for the given inputs." };
            } catch (Exception ex) {
                _logger?.LogError(ex.StackTrace);
                return new Feedback().SetMessage(ex.StackTrace);
            }
        }

        async Task<IFeedback> GetDocVersionInfoInternal(string moduleCuid, long id, string cuid) {
            Feedback result = new Feedback();
            try {
                if (string.IsNullOrWhiteSpace(moduleCuid)) return result.SetMessage($@"Module CUID is mandatory to fetch document info");
                if (id < 1 && string.IsNullOrWhiteSpace(cuid)) return result.SetMessage($@"Either Id or CUID value is required.");
                if (!_agw.ContainsKey(moduleCuid)) return result.SetMessage($@"No adapter found for the key {moduleCuid}");

                var query = id < 1 ? INSTANCE.DOCVERSION.GET_FULL_BY_CUID : INSTANCE.DOCVERSION.GET_FULL_BY_ID;
                var data =await _agw.Read(new AdapterArgs(moduleCuid) { Query = query, Filter = ResultFilter.FirstDictionary }, (VALUE, id < 1 ? cuid : id));
                if (data == null || !(data is Dictionary<string, object> dic) || dic.Count < 1) return result.SetMessage($@"Unable to fetch the document version info with either cuid {cuid} or id {id}");
                return result.SetStatus(true).SetMessage("Document version info obtained").SetResult(dic);
            } catch (Exception ex) {
                _logger?.LogError(ex.StackTrace);
                return result.SetMessage(ex.StackTrace);
            }
        }
    }
}
