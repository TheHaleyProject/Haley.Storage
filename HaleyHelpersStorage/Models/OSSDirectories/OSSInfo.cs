using Haley.Abstractions;
using Haley.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Haley.Enums;
using System.Linq;

namespace Haley.Models {
    public class OSSInfo : IOSSInfo {
        public const string DEFAULTNAME = "default";
        public string Name { get; private set; }
        private string _displayName;
        public string DisplayName {
            get { return _displayName; }
            set {
                if (!string.IsNullOrWhiteSpace(value)) {
                    _displayName = value.Trim();
                } else {
                    _displayName = DEFAULTNAME;
                }
                if (!ValidateInternal(out var msg)) throw new Exception(msg);
                Name = _displayName.ToDBName(); //Db compatible name
                Guid = Name.CreateGUID(HashMethod.Sha256).ToString("N");
            }
        }

        public virtual bool TryValidate(out string message) {
            return ValidateInternal(out message);
        }

        //public IOSSInfo SetCUID(string uid) {
        //    if (string.IsNullOrWhiteSpace(uid) || !uid.IsCompactGuid(out _)) return this;
        //    Cuid = uid;
        //    return this;
        //}

        bool ValidateInternal(out string message) {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(DisplayName)) {
                message = "Display Name cannot be empty";
                return false;
            }
            if (DisplayName.Contains("..") || DisplayName.Contains(@"\") || DisplayName.Contains(@"/")) {
                message = "Name contains invalid characters";
                return false;
            }
            return true;
        }

        protected virtual void GenerateCuid() {
            Cuid = OSSUtils.GenerateCuid(DisplayName);
        }

        public virtual IOSSInfo UpdateCUID(params string[] parentNames) {
            if (parentNames == null) return this;
            var inputList = parentNames.ToList();
            if (inputList.Count == 0 || inputList.Last().ToDBName() != Name) {
                inputList.Add(Name); //I
            }
            Cuid = OSSUtils.GenerateCuid(inputList.ToArray());
            return this;
        }

        public string Guid { get; private set; } //Sha256 generated from the name and a guid is created from there.
        [IgnoreMapping]
        public string Cuid { get; protected set; } //Collision resistant Unique identifier
        public OSSInfo(string displayName) {
            DisplayName = displayName ?? DEFAULTNAME;
        }
    }
}
