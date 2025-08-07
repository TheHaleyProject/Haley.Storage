using Haley.Abstractions;
using Haley.Enums;
using Haley.Utils;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Xml.Linq;

namespace Haley.Models {
    public class OSSControlled :OSSInfo , IOSSControlled{
        public string SaveAsName { get; set; } //Should be the controlled name or a name compatible for the database 
        public bool IsVirtual { get; set; }
        public OSSControlMode ControlMode { get; set; } //Parsing or create mode is defined at application level?
        public OSSParseMode ParseMode { get; set; } //If false, we fall back to parsing.
        public virtual IOSSControlled UpdateCUID(params string[] parentNames) {
            if (parentNames == null) return this;
            var inputList = parentNames.ToList() ;
            if (inputList.Count == 0 || inputList.Last().ToDBName() != Name) {
                inputList.Add(Name); //I
            }
            Cuid = OSSUtils.GenerateCuid(inputList.ToArray());
            return this;
        }
        public OSSControlled(string displayname, OSSControlMode control = OSSControlMode.None, OSSParseMode parse = OSSParseMode.Parse, bool isVirtual = false) : base(displayname) {
            ControlMode = control;
            ParseMode = parse;
            GenerateCuid();
            IsVirtual = isVirtual;
        }
    }
}
