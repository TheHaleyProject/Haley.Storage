using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;

namespace Haley.Services {
    public partial class DiskStorageService : IDiskStorageService {
        public Task<IFeedback> RegisterClient(string name, string password = null) {
            return RegisterClient(new OSSControlled(name));
        }
        public Task<IFeedback> RegisterModule(string name, string client_name = null) {
            return RegisterModule(new OSSControlled(name), new OSSControlled(client_name));
        }
        public async Task<IFeedback> RegisterClient(IOSSControlled input, string password = null) {
            //Password will be stored in the .dss.meta file
            if (input == null) return new Feedback(false, "Name cannot be empty");
            if (!input.TryValidate(out var msg)) return new Feedback(false, msg);
            if (input.ControlMode != OSSControlMode.None) input.ControlMode = OSSControlMode.Guid; //Either we allow as is, or we go with GUID. no numbers allowed.
            if (string.IsNullOrWhiteSpace(password)) password = DEFAULTPWD;
            var cInput = GenerateBasePath(input, OSSComponent.Client); //For client, we only prefer hash mode.
            var path = Path.Combine(BasePath, cInput.path);

            //Thins is we are not allowing any path to be provided by user. Only the name is allowed.

            //Create these folders and then register them.
            if (!Directory.Exists(path) && WriteMode) {
                Directory.CreateDirectory(path); //Create the directory only if write mode is enabled or else, we just try to store the information in cache.
            }

            var signing = RandomUtils.GetString(512);
            var encrypt = RandomUtils.GetString(512);
            var pwdHash = HashUtils.ComputeHash(password, HashMethod.Sha256);
            var result = new Feedback(true, $@"Client {input.DisplayName} is registered");

            var clientInfo = input.MapProperties(new OSSClient(pwdHash, signing, encrypt) { Path = cInput.path });
            if (WriteMode) {
                var metaFile = Path.Combine(path, CLIENTMETAFILE);
                File.WriteAllText(metaFile, clientInfo.ToJson());   // Over-Write the keys here.
            }

            if (!Directory.Exists(path)) result.SetStatus(false).SetMessage("Directory was not created. Check if WriteMode is ON Or make sure proper access is availalbe");

            if (!result.Status || Indexer == null) return result;
            var idxResult = await Indexer.RegisterClient(clientInfo);
            result.Result = idxResult.Result;
            return result;
        }
        public async Task<IFeedback> RegisterModule(IOSSControlled input, IOSSControlled client) {
            //AssertValues(true, (client_name,"client name"), (name,"module name")); //uses reflection and might carry performance penalty
            string msg = string.Empty;
            if (!input.TryValidate(out msg)) new Feedback(false, msg);
            if (!client.TryValidate(out msg)) new Feedback(false, msg);

            var client_path = GenerateBasePath(client, OSSComponent.Client).path; //For client, we only prefer hash mode.
            var bPath = Path.Combine(BasePath, client_path);
            if (!Directory.Exists(bPath)) return new Feedback(false, $@"Directory not found for the client {client.DisplayName}");
            if (client_path.Contains("..")) return new Feedback(false, "Client Path contains invalid characters");

            //MODULE INFORMATION BASIC VALIDATION
            var modPath = GenerateBasePath(input, OSSComponent.Module).path; //For client, we only prefer hash mode.
            bPath = Path.Combine(bPath, modPath); //Including Client Path

            //Create these folders and then register them.
            if (!Directory.Exists(bPath) && WriteMode) {
                Directory.CreateDirectory(bPath); //Create the directory.
            }

            var moduleInfo = input.MapProperties(new OSSModule(client.Name) { Path = modPath });
            if (WriteMode) {
                var metaFile = Path.Combine(bPath, MODULEMETAFILE);
                File.WriteAllText(metaFile, moduleInfo.ToJson());
            }

            var result = new Feedback(true, $@"Module {input.DisplayName} is registered");
            if (!Directory.Exists(bPath)) result.SetStatus(false).SetMessage("Directory is not created. Please ensure if the WriteMode is turned ON or proper access is availalbe.");

            if (Indexer == null) return result;
            var idxResult = await Indexer.RegisterModule(moduleInfo);
            result.Result = idxResult.Result;
            return result;
        }
        public Task<IFeedback> RegisterWorkSpace(string name, string client_name = null, string module_name = null) {
            return RegisterWorkSpace(name, client_name, module_name);
        }
        public Task<IFeedback> RegisterWorkSpace(string name, string client_name, string module_name, OSSControlMode content_control = OSSControlMode.None, OSSParseMode content_pmode = OSSParseMode.Parse) {
            return RegisterWorkSpace(new OSSControlled(name, OSSControlMode.Guid, OSSParseMode.ParseOrGenerate), new OSSControlled(client_name), new OSSControlled(module_name), content_control, content_pmode);
        }
        public async Task<IFeedback> RegisterWorkSpace(IOSSControlled input, IOSSControlled client, IOSSControlled module, OSSControlMode content_control = OSSControlMode.None, OSSParseMode content_pmode = OSSParseMode.Parse) {
            string msg = string.Empty;
            if (!input.TryValidate(out msg)) throw new Exception(msg);
            if (!client.TryValidate(out msg)) throw new Exception(msg);
            if (!module.TryValidate(out msg)) throw new Exception(msg);

            var cliPath = GenerateBasePath(client, OSSComponent.Client).path;
            var modPath = GenerateBasePath(module, OSSComponent.Module).path;

            var bpath = Path.Combine(BasePath, cliPath, modPath);
            if (!Directory.Exists(bpath)) return new Feedback(false, $@"Unable to lcoate the basepath for the Client : {client.DisplayName}, Module : {module.DisplayName}");
            if (bpath.Contains("..")) return new Feedback(false, "Invalid characters found in the base path.");

            //MODULE INFORMATION BASIC VALIDATION
            var wsPath = GenerateBasePath(input, OSSComponent.WorkSpace).path; //For client, we only prefer hash mode.
            var path = Path.Combine(bpath, wsPath); //Including Base Paths

            //Create these folders and then register them.
            if (!Directory.Exists(path) && WriteMode) {
                Directory.CreateDirectory(path); //Create the directory.
            }

            var wsInfo = input.MapProperties(new OSSWorkspace(client.Name, module.Name, input.DisplayName) { Path = wsPath });
            if (WriteMode) {
                var metaFile = Path.Combine(path, WORKSPACEMETAFILE);
                File.WriteAllText(metaFile, wsInfo.ToJson());
            }

            var result = new Feedback(true, $@"Workspace {input.DisplayName} is registered");
            if (!Directory.Exists(path)) result.SetStatus(false).SetMessage("Directory is not created. Please ensure if the WriteMode is turned ON or proper access is availalbe.");

            if (Indexer == null) return result;
            var idxResult = await Indexer.RegisterWorkspace(wsInfo);
            result.Result = idxResult.Result;
            return result;
        }

        public Task<IFeedback> AuthorizeClient(object clientInfo, object clientSecret) {
            //Take may be we take the password? no?
            //We can take the password for this client, and compare with the information available in the DB or in the folder. 
            //Whenever indexing is enabled, may be we need to take all the availalbe clients and fetch their password file and update the DB. Because during the time the indexing was down, may be system generated it's own files and stored it.
            IFeedback result = new Feedback();
            result.Status = true;
            result.Message = "No default implementation available. All requests authorized.";
            return Task.FromResult(result);
        }
    }
}
