using Haley.Abstractions;
using System.Collections.Generic;
using Haley.Enums;

namespace Haley.Models {
    public class OSSReadFile : OSSReadRequest, IOSSReadFile {
        public IOSSFileRoute File { get; set; }  
        public OSSReadFile() : base(){ }
        public OSSReadFile(string client_name) : base(client_name) { }
        public OSSReadFile(string client_name,string module_name) : base(client_name, module_name) { }
        public OSSReadFile(string client_name, string module_name, string workspace_name, bool isWsVirtual = false) : base(client_name,module_name,workspace_name,isWsVirtual) {
        }
    }
}
