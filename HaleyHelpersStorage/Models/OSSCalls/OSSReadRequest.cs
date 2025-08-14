using Haley.Abstractions;
using System.Collections.Generic;
using Haley.Enums;
using Haley.Utils;

namespace Haley.Models {
    public class OSSReadRequest : IOSSRead {
        bool callIdGenerated;
        public string CallID { get; protected set; } = Guid.NewGuid().ToString();
        public string TargetPath { get; protected set; }
        public string TargetName { get; protected set; }
        public IOSSControlled Client { get; protected set; } 
        public IOSSControlled Module { get; protected set; }
        public IOSSControlled Workspace { get; protected set; } 
        public IOSSFolderRoute Folder { get; protected set; }
        public bool ReadOnlyMode { get; protected set; }
        public bool GenerateCallId() {
            if (callIdGenerated) return false;
            CallID = Guid.NewGuid().ToString();
            callIdGenerated = true;
            return true;
        }

        public virtual IOSSRead SetComponent(IOSSControlled input, OSSComponent type) {
            switch (type) {
                case OSSComponent.Client:
                    Client = input;
                break;
                case OSSComponent.Module:
                    Module = input; 
                break;
                case OSSComponent.WorkSpace:
                    Workspace = input;
                break;
            }
            UpdateCUID();
            return this;
        }
        void UpdateCUID() {
            if (Client == null) return;
            if (Module != null) Module.UpdateCUID(Client.DisplayName);
            if (Workspace != null) Workspace.UpdateCUID(Client.DisplayName, Module?.DisplayName);
        }

        public IOSSRead SetTargetName(string name) {
            if (string.IsNullOrWhiteSpace(name)) return this;
            TargetName = name;
            return this;
        }
        public IOSSRead SetFolder(IOSSFolderRoute folder) {
            if (folder != null) Folder = folder;
            return this;
        }
        public IOSSRead SetTargetPath(string path) {
            if (string.IsNullOrWhiteSpace(path)) return this;
            TargetPath = path;
            return this;
        }

        public IOSSRead SetMode(bool readOnly) {
            ReadOnlyMode = readOnly;
            return this;
        }

        public OSSReadRequest() :this (null,null,null){ }
        public OSSReadRequest(string client_name) :this(client_name,null,null) { }
        public OSSReadRequest(string client_name,string module_name) :this(client_name, module_name, null) { }

        public  OSSReadRequest(string client_name, string module_name, string workspace_name) {
            Client = new OSSControlled(client_name);
            Module = new OSSControlled(module_name).UpdateCUID(Client.DisplayName,module_name);
            Workspace = new OSSControlled(workspace_name).UpdateCUID(Client.DisplayName,Module.DisplayName); //Here nothing matters, because it is an input request. // We need to fetch the information from database and then update this workspace information.
        }
    }
}
