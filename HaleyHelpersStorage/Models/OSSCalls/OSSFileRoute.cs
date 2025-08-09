using Haley.Abstractions;

namespace Haley.Models {
    //When using a struct, remember that it is a value type.
    //Thus, when you modify it, you need to return the modified struct.
    //This is useful for immutability and functional programming paradigms.
    public class OSSFileRoute : OSSRoute, IOSSFileRoute {
        public int Version { get; set; } = 0;
        public long Size { get; set; } = 0;
        public OSSFileRoute(string name, string path) : base(name,path) { 
        }
    }
}
