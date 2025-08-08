using Haley.Abstractions;
using Haley.Models;
using Haley.Utils;
using Microsoft.Extensions.Logging;

namespace Haley.Services {
    public partial class DiskStorageService : IDiskStorageService {

        bool _isInitialized = false;
        ILogger _logger;
        const string METAFILE = ".dss.meta";
        const string CLIENTMETAFILE = ".client" + METAFILE;
        const string MODULEMETAFILE = ".module" + METAFILE;
        const string WORKSPACEMETAFILE = ".ws" + METAFILE;
        const string DEFAULTPWD = "admin";
        public IDSSConfig Config { get; set; } = new DSSConfig();
        public DiskStorageService(bool write_mode = true, ILogger logger = null, bool throwExceptions = false) : this(null, null, write_mode,logger,throwExceptions) {
        }
        public DiskStorageService(string basePath, bool write_mode = true, ILogger logger = null, bool throwExceptions = false) : this(basePath, write_mode, null, throwExceptions, logger) { }
        public DiskStorageService(IAdapterGateway agw, string adapter_key, bool write_mode = true, ILogger logger = null, bool throwExceptions = false) : this(null, write_mode, new MariaDBIndexing(agw, adapter_key, logger,throwExceptions), throwExceptions, logger) { }
        public DiskStorageService(IAdapterGateway agw, string adapter_key, string basePath, bool write_mode = true, ILogger logger = null,bool throwExceptions = false) : this(basePath, write_mode, new MariaDBIndexing(agw, adapter_key, logger,throwExceptions) { }, throwExceptions, logger) { }
        public DiskStorageService(string basePath, bool write_mode, IDSSIndexing indexer, bool throwExceptions, ILogger logger =null) {
            BasePath = basePath?.Trim();
            WriteMode = write_mode;
            ThrowExceptions = throwExceptions;
            //This is supposedly the directory where all storage goes into.
            if (string.IsNullOrWhiteSpace(BasePath)) {
                BasePath = AssemblyUtils.GetBaseDirectory(parentFolder: "DataStore");
            }
            BasePath = BasePath?.ToLower();
            SetIndexer(indexer);
            _logger = logger;

            //If a client is not registered, do we need to register the default client?? and a default module??
        }
        async Task Initialize(bool force = false) {
            if (_isInitialized && !force) return;
            var defObj = new OSSControlled(OSSInfo.DEFAULTNAME);
            await RegisterClient(defObj); //Registers defaul client, with default module and default workspace
            _isInitialized = true;
        }

        public bool ThrowExceptions { get; set; }
        public string BasePath { get; }
        public bool WriteMode { get; set; }
        IDSSIndexing Indexer;

        public DiskStorageService SetWriteMode(bool mode) {
            WriteMode = mode;
            return this;
        }
        public IDiskStorageService SetIndexer(IDSSIndexing service) {
            Indexer = service;
            Initialize(true).Wait();
            return this;
        }
    }
}
