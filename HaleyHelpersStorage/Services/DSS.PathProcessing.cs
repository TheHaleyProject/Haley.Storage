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
                var suffixAddon = input.ControlMode == OSSControlMode.None ? "u" : "m";
                suffix = suffixAddon + Config.SuffixWorkSpace;
                length = 2; depth = 2;
                break;
                case OSSComponent.File:
                suffix = Config.SuffixFile;
                break;
            }
            return OSSUtils.GenerateFileSystemSavePath(input, OSSParseMode.ParseOrGenerate, (n) => { return (length, depth); }, suffix: suffix, throwExceptions: false);
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
        public void EnsureStorageRoutes(IOSSRead input) {
            //The last storage route should be in the format of a file
            if (input.StorageRoutes.Count < 1 || !input.StorageRoutes.Last().IsFile) {
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
                    }
                } else {
                    throw new ArgumentNullException("For the given file no save name is specified.");
                }

                //Now, this targetFileName, may or may not be split based on what was defined in the module.
                //Check the module info.
                if (Indexer.TryGetComponentInfo<OSSWorkspace>(OSSUtils.GenerateCuid(input, OSSComponent.WorkSpace), out OSSWorkspace wInfo)) {
                    //TODO: USE THE INDEXER TO GET THE PATH FOR THIS SPECIFIC FILE WITH MODULE AND CLIENT NAME.
                    //TODO: IF THE PATH IS OBTAINED, THEN JUST JOIN THE PATHS.
                    targetFilePath = OSSUtils.GenerateFileSystemSavePath(new OSSControlled(targetFileName, wInfo.ContentControl, wInfo.ContentParse,isVirtual:false), splitProvider: SplitProvider, suffix: Config.SuffixFile, throwExceptions: true).path;
                } else {
                    targetFilePath = targetFileName.ToDBName(); //Just lower it 
                }
                input.StorageRoutes.Add(new OSSRoute(targetFileName, targetFilePath, true, false));
            }
        }
        public (string basePath, string targetPath) ProcessAndBuildStoragePath(IOSSRead input, bool ensureFileRoute = true, bool allowRootAccess = false, bool readonlyMode = false) {
            var bpath = FetchBasePath(input);
            if (ensureFileRoute) EnsureStorageRoutes(input);
            var path = input?.BuildStoragePath(bpath, allowRootAccess, readonlyMode); //This will also ensure we are not trying to delete something 
            return (bpath, path);
        }
    }
}
