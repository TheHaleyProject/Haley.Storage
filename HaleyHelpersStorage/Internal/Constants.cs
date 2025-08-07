using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml.Linq;
using static Haley.Internal.IndexingConstant;

namespace Haley.Internal {
    internal class IndexingConstant {
        public const string VAULT_DEFCLIENT = "admin";
        public const string NAME = $@"@{nameof(NAME)}";
        public const string DNAME = $@"@{nameof(DNAME)}";
        public const string GUID = $@"@{nameof(GUID)}";
        public const string CUID = $@"@{nameof(CUID)}";
        public const string PATH = $@"@{nameof(PATH)}";
        public const string SUFFIX_DIR = $@"@{nameof(SUFFIX_DIR)}";
        public const string SUFFIX_FILE = $@"@{nameof(SUFFIX_FILE)}";
        public const string ID = $@"@{nameof(ID)}";
        public const string FULLNAME = $@"@{nameof(FULLNAME)}";
        public const string SIGNKEY = $@"@{nameof(SIGNKEY)}";
        public const string ENCRYPTKEY = $@"@{nameof(ENCRYPTKEY)}";
        public const string VALUE = $@"@{nameof(VALUE)}";
        public const string PASSWORD = $@"@{nameof(PASSWORD)}";
        public const string DATETIME = $@"@{nameof(DATETIME)}";
        public const string PARENT = $@"@{nameof(PARENT)}";
        public const string CONTROLMODE = $@"@{nameof(CONTROLMODE)}";
        public const string PARSEMODE = $@"@{nameof(PARSEMODE)}";
    }

    internal class IndexingQueries {
        public class CLIENT {
            public const string EXISTS = $@"select c.id from client as c where c.name = {NAME} LIMIT 1;";
            public const string UPSERTKEYS = $@"insert into client_keys (client,signing,encrypt,password) values ({ID},{SIGNKEY},{ENCRYPTKEY},{PASSWORD}) ON DUPLICATE KEY UPDATE signing =  VALUES(signing), encrypt = VALUES(encrypt), password = VALUES(password);";
            public const string UPSERT = $@"insert into client (name,display_name, guid,path) values ({NAME},{DNAME},{GUID},{PATH}) ON DUPLICATE KEY UPDATE display_name = VALUES(display_name), path = VALUES(path);";
            public const string UPDATE = $@"update client set display_name = {DNAME}, path = {PATH} where id = {ID};";
            public const string GETKEYS = $@"select * from client_keys as c where c.client = {ID} LIMIT 1;";
        }
        
        public class MODULE {
            public const string EXISTS = $@"select m.id from module as m where m.name = {NAME} and m.parent = {PARENT} LIMIT 1;";
            public const string EXISTS_BY_CUID = $@"select m.id from module as m where m.cuid = {CUID} LIMIT 1;";
            public const string UPSERT = $@"insert into module (parent,name, display_name,guid,path,cuid) values ({PARENT}, {NAME},{DNAME},{GUID},{PATH},{CUID}) ON DUPLICATE KEY UPDATE display_name = VALUES(display_name), path = VALUES(path);";
            public const string UPDATE = $@"update module set display_name = {DNAME}, path = {PATH} where id = {ID};";
        }
        public class WORKSPACE {
            public const string EXISTS = $@"select ws.id from workspace as ws where ws.name = {NAME} and ws.parent = {PARENT} LIMIT 1;";
            public const string EXISTS_BY_CUID = $@"select ws.id from workspace as ws where ws.cuid = {CUID} LIMIT 1;";
            public const string UPSERT = $@"insert into workspace (parent,name, display_name,guid,path,cuid,control_mode,parse_mode) values ({PARENT}, {NAME},{DNAME},{GUID},{PATH},{CUID},{CONTROLMODE},{PARSEMODE}) ON DUPLICATE KEY UPDATE display_name = VALUES(display_name), path = VALUES(path),control_mode=VALUES(control_mode),parse_mode=VALUES(parse_mode);";
            public const string UPDATE = $@"update workspace set display_name = {DNAME}, path = {PATH},control_mode={CONTROLMODE},parse_mode={PARSEMODE} where id = {ID};";
        }
    }
}
