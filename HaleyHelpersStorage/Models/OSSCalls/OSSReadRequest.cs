using Haley.Abstractions;
using System.Collections.Generic;
using Haley.Enums;

namespace Haley.Models {
    public class OSSReadRequest : IOSSRead {
        public string TargetPath { get; set; }
        public string TargetName { get; set; }
        public IOSSControlled Client { get; private set; } 
        public IOSSControlled Module { get; private set; }
        public IOSSControlled Workspace { get; private set; } 
        public int Version { get; set; } = 0; //Send latest
        public List<OSSRoute> StorageRoutes { get; } = new List<OSSRoute>(); //Initialization. We can only then clear, or Add.
        public virtual OSSReadRequest SetComponent(OSSControlled input, OSSComponent type) {
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
        public OSSReadRequest() :this (null,null,null){ }
        public OSSReadRequest(string client_name) :this(client_name,null,null) { }
        public OSSReadRequest(string client_name,string module_name) :this(client_name, module_name, null) { }

        public  OSSReadRequest(string client_name, string module_name, string workspace_name, bool isWsVirtual = false) {
            Client = new OSSControlled(client_name);
            Module = new OSSControlled(module_name).UpdateCUID(Client.DisplayName);
            Workspace = new OSSControlled(workspace_name,OSSControlMode.Both,OSSParseMode.ParseOrGenerate,isVirtual:isWsVirtual).UpdateCUID(Client.DisplayName,Module.DisplayName);
        }
    }
}
