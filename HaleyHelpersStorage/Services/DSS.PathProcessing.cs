using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;

namespace Haley.Services {
    public partial class DiskStorageService : IDiskStorageService {
        public (string name, string path) GenerateBasePath(IOSSControlled input, OSSComponent basePath) {
            string suffix = string.Empty;
            int length = 2;
            int depth = 0;
            switch (basePath) {
                case OSSComponent.Client: //We might have very limited number of clients.
                suffix = Config.SuffixClient;
                length = 0; depth = 0;
                break;
                case OSSComponent.Module:
                suffix = Config.SuffixModule;
                length = 0; depth = 0;
                break;
                case OSSComponent.WorkSpace:
                //Only if the parase mode is generate, it is managed.
                string suffixAddon =string.Empty;
                if (input.ControlMode == OSSControlMode.None) {
                    suffixAddon = "u"; //Fully unmanagaed.
                } else {
                    if (input.ParseMode == OSSParseMode.Generate) {
                        suffixAddon = "f"; //Fully unmanagaed. //Ids are generated from the database and everthing is controlled there.
                    } else {
                        suffixAddon = "p"; //Partially managed. Folder structures are properly managed with split, but, will not store any other file information. File conflicts and other things are possible. No directory structure allowed.
                    }
                }
                suffix = suffixAddon + Config.SuffixWorkSpace;
                length = 1; depth = 5;
                break;
                case OSSComponent.File:
                suffix = Config.SuffixFile;
                throw new NotImplementedException("No method implemented for handling BasePath generation for File Component type.");
            }
            return OSSUtils.GenerateFileSystemSavePath(input, OSSParseMode.Generate, (n) => { return (length, depth); }, suffix: suffix, throwExceptions: false);
        }
        public string GetStorageRoot() {
            return BasePath;
        }

        (IOSSControlled target, OSSComponent type, string metaFilePath, string cuid) GetTargetInfo<T>(IOSSRead input) where T : IOSSDirectory {
            IOSSControlled target = null;
            OSSComponent targetType = OSSComponent.Client;
            string metaFilePath = string.Empty;

            if (typeof(IOSSClient).IsAssignableFrom(typeof(T))) {
                targetType = OSSComponent.Client;
                metaFilePath = CLIENTMETAFILE;
                target = input.Client;
            } else if (typeof(IOSSModule).IsAssignableFrom(typeof(T))) {
                targetType = OSSComponent.Module;
                metaFilePath = MODULEMETAFILE;
                target = input.Module;
            } else if (typeof(IOSSWorkspace).IsAssignableFrom(typeof(T))) {
                targetType = OSSComponent.WorkSpace;
                metaFilePath = WORKSPACEMETAFILE;
                target = input.Workspace;
            }

            string cuid = OSSUtils.GenerateCuid(input, targetType);
            return (target, targetType, metaFilePath, cuid);
        }

        void FetchBasePath<T>(IOSSRead input, List<string> paths) where T : IOSSDirectory {
            //We try to take the paths for all the components.
            if (paths == null) paths = new List<string>();

            if (Indexer == null) return;
            var info = GetTargetInfo<T>(input);

            if (info.target != null) {
                if (Indexer.TryGetComponentInfo(info.cuid, out T obj)) {
                    if (!string.IsNullOrWhiteSpace(obj.Path)) paths.Add(obj.Path);  //Because sometimes we might have modules or clients where we dont' ahve any path specified. So , in those cases, we just ignore them.
                } else {
                    var tuple = GenerateBasePath(info.target, info.type);//here, we are merely generating a path based on what the user has provided. It doesn't mean that such a path really exists . 
                    paths.Add(tuple.path);
                    try {
                        var metafile = Path.Combine(BasePath, tuple.path, info.metaFilePath);
                        if (File.Exists(metafile)) {
                            //File exists , gives us the password, encrypt key and everything.. if not available already in the database cache.
                            var mfileInfo = File.ReadAllText(metafile).FromJson<T>();
                            if (mfileInfo != null) {
                                Indexer?.TryAddInfo(mfileInfo);
                            }
                        }
                    } catch (Exception) {
                    }
                }
            }
        }

        string FetchBasePath(IOSSRead request) {
            Initialize().Wait(); //To ensure base folders are created.
            string result = BasePath;
            List<string> paths = new List<string>();
            paths.Add(BasePath);
            FetchBasePath<OSSClient>(request, paths);
            FetchBasePath<OSSModule>(request, paths);
            FetchBasePath<OSSWorkspace>(request, paths);
            if (paths.Count > 0) result = Path.Combine(paths.ToArray());
            if (!Directory.Exists(result)) throw new DirectoryNotFoundException("The base path doesn't exists.. Unable to build the base path from given input.");
            return result;
        }
        public (int length, int depth) SplitProvider(bool isNumber) {
            if (isNumber) return (Config.SplitLengthNumber, Config.DepthNumber);
            return (Config.SplitLengthHash, Config.DepthHash);
        }
        public void ProcessFileRoute(IOSSRead input) {
            //The last storage route should be in the format of a file
            if (input != null &&  input.File == null || string.IsNullOrWhiteSpace(input.File.Path)) {
                //We are trying to upload a file but the last storage route is not in the format of a file.
                //We need to see if the filestream is present and take the name from there.
                //Priority for the name comes from TargetName
                string targetFileName = string.Empty;
                string targetFilePath = string.Empty;
                if (!string.IsNullOrWhiteSpace(input.TargetName)) {
                    targetFileName = Path.GetFileName(input.TargetName);
                } else if (input is IOSSWrite inputW) {
                    if (!string.IsNullOrWhiteSpace(inputW.FileOriginalName)) {
                        targetFileName = Path.GetFileName(inputW.FileOriginalName);
                    } else if (inputW.FileStream != null && inputW.FileStream is FileStream fs) {
                        targetFileName = Path.GetFileName(fs.Name);
                        if (string.IsNullOrWhiteSpace(inputW.FileOriginalName)) inputW.SetFileOriginalName(targetFileName);
                    }
                } else {
                    throw new ArgumentNullException("For the given file no save name is specified.");
                }

                if (string.IsNullOrWhiteSpace(input.TargetName)) input.SetTargetName(targetFileName);
                var workspaceCuid = OSSUtils.GenerateCuid(input, OSSComponent.WorkSpace);
                //If a component information is not avaialble for the workspace, we should not proceed.
                if (!Indexer.TryGetComponentInfo<OSSWorkspace>(workspaceCuid, out OSSWorkspace wInfo)) throw new Exception($@"Unable to find the workspace information for the given input. Workspace name : {input.Workspace.Name} - Cuid : {workspaceCuid}.");

                //TODO: USE THE INDEXER TO GET THE PATH FOR THIS SPECIFIC FILE WITH MODULE AND CLIENT NAME.
                //TODO: IF THE PATH IS OBTAINED, THEN JUST JOIN THE PATHS.
                var holder = new OSSControlled(targetFileName, wInfo.ContentControl, wInfo.ContentParse, isVirtual: false);
                targetFilePath = OSSUtils.GenerateFileSystemSavePath(
                    holder,
                    uidManager: (h) => { return Indexer?.UIDManager(input,h) ?? (0,Guid.Empty); },
                    splitProvider: SplitProvider,
                    suffix: Config.SuffixFile,
                    throwExceptions: true)
                    .path;

                if (input.File == null) {
                    input.File = new OSSFileRoute(targetFileName, targetFilePath) { Id = holder.Id, Cuid = holder.Cuid, Version = holder.Version};
                }

                input.File.Path = targetFilePath;
                if (string.IsNullOrWhiteSpace(input.File.Name)) input.File.Name = targetFileName;
                if (string.IsNullOrWhiteSpace(input.File.Cuid)) input.File.Cuid = holder.Cuid;
                if (input.File.Id < 1) input.File.Id = holder.Id;
            }
        }

        public (string basePath, string targetPath) ProcessAndBuildStoragePath(IOSSRead input, bool for_file , bool allowRootAccess = false, bool readonlyMode = false) {
            var bpath = FetchBasePath(input);
            if (for_file) ProcessFileRoute(input);
            var path = input?.BuildStoragePath(bpath,for_file, allowRootAccess, readonlyMode); //This will also ensure we are not trying to delete something 
            return (bpath, path);
        }
    }
}
