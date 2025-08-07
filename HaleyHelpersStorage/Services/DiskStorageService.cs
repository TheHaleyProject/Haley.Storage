using Haley.Abstractions;
using Haley.Models;
using Haley.Utils;

namespace Haley.Services {
    public partial class DiskStorageService : IDiskStorageService {
        bool _isInitialized = false;
        const string METAFILE = ".dss.meta";
        const string CLIENTMETAFILE = ".client" + METAFILE;
        const string MODULEMETAFILE = ".module" + METAFILE;
        const string WORKSPACEMETAFILE = ".ws" + METAFILE;
        const string DEFAULTPWD = "admin";
        public IDSSConfig Config { get; set; } = new DSSConfig();
        public DiskStorageService(bool write_mode = true) : this(null, null, write_mode) { }
        public DiskStorageService(string basePath, bool write_mode = true) : this(basePath, write_mode, null) { }
        public DiskStorageService(IAdapterGateway agw, string adapter_key, bool write_mode = true) : this(null, write_mode, new MariaDBIndexing(agw, adapter_key)) { }
        public DiskStorageService(IAdapterGateway agw, string adapter_key, string basePath, bool write_mode = true) : this(basePath, write_mode, new MariaDBIndexing(agw, adapter_key)) { }
        public DiskStorageService(string basePath, bool write_mode, IDSSIndexing indexer) {
            BasePath = basePath;
            WriteMode = write_mode;
            //This is supposedly the directory where all storage goes into.
            if (BasePath == null) {
                BasePath = AssemblyUtils.GetBaseDirectory(parentFolder: "DataStore");
            }
            BasePath = BasePath?.ToLower();
            SetIndexer(indexer);
            //If a client is not registered, do we need to register the default client?? and a default module??
        }
        async Task Initialize(bool force = false) {
            if (_isInitialized && !force) return;
            await RegisterClient(new OSSControlled()); //Registers defaul client
            await RegisterModule(new OSSControlled(), new OSSControlled()); //Registers default module
            _isInitialized = true;
        }
        public string BasePath { get; }
        public bool WriteMode { get; set; }
        IDSSIndexing Indexer;

        public DiskStorageService SetWriteMode(bool mode) {
            WriteMode = mode;
            return this;
        }
        public IDiskStorageService SetIndexer(IDSSIndexing service) {
            Indexer = service;
            return this;
        }
    }
}
