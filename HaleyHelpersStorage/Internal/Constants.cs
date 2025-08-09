﻿using System;
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
        public const string SAVENAME = $@"@{nameof(SAVENAME)}";
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
        public const string WSPACE = $@"@{nameof(WSPACE)}";
        public const string EXT = $@"@{nameof(EXT)}";
        public const string VERSION = $@"@{nameof(VERSION)}";
        public const string SIZE = $@"@{nameof(SIZE)}";
    }

    internal class IndexingQueries {
        public class GENERAL {
            public const string SCHEMA_EXISTS = $@"select 1 from information_schema.schemata where schema_name = {NAME};";
        }
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

        public class INSTANCE {
           public class WORKSPACE {
                public const string EXISTS = $@"select w.id from workspace as w where w.id = {ID};";
                public const string INSERT = $@"insert IGNORE into workspace (id) values ({ID});";
           }

            public class DIRECTORY {
                public const string EXISTS = $@"select dir.id, dir.cuid as uid from directory as dir where dir.workspace = {WSPACE} and dir.parent = {PARENT} and dir.name = {NAME};";
                public const string EXISTS_BY_CUID = $@"select dir.id from directory as dir where dir.cuid = {CUID};";
                public const string INSERT = $@"insert ignore into directory (workspace,parent,name,display_name) values ({WSPACE},{PARENT},{NAME},{DNAME});";
            }
            public class EXTENSION {
                public const string EXISTS = $@"select ext.id from extension as ext where ext.name = {NAME};";
                public const string INSERT = $@"insert ignore into extension (name) values ({NAME});";
            }

            public class VAULT {
                public const string EXISTS = $@"select v.id from vault as v where v.name = {NAME};";
                public const string INSERT = $@"insert ignore into vault (name) values ({NAME});";
            }

            public class NAMESTORE {
                public const string EXISTS = $@"select ns.id from name_store as ns where ns.name = {NAME} and ns.extension = {EXT};";
                public const string INSERT = $@"insert ignore into name_store (name,extension) values ({NAME},{EXT});";
            }

            public class DOCUMENT {
                public const string EXISTS = $@"select doc.id , doc.cuid as uid from document as doc where doc.parent = {PARENT} and doc.name = {NAME};";
                public const string EXISTS_BY_CUID = $@"select doc.id , doc.cuid as uid from document as doc where doc.cuid = {CUID};";
                public const string INSERT = $@"insert ignore into document (workspace,parent,name) values ({WSPACE},{PARENT},{NAME});";
                public const string INSERT_INFO = $@"insert into doc_info (file,display_name) values ({PARENT}, {DNAME}) ON DUPLICATE KEY UPDATE display_name = VALUES(display_name);";
            }
            
            public class DOCVERSION {
                public const string EXISTS = $@"select dv.id , dv.cuid as uid from doc_version as dv where dv.parent = {PARENT} and dv.ver = {VERSION};";
                public const string EXISTS_BY_CUID = $@"select dv.id , dv.cuid from doc_version as dv where dv.cuid = {CUID};";
                public const string INSERT = $@"insert ignore into doc_version (parent,ver) values({PARENT},{VERSION});";
                public const string INSERT_INFO = $@"insert into version_info (saveas_name,path) values({SAVENAME},{PATH});";
                public const string FIND_LATEST = $@"select MAX(dv.ver) from doc_version as dv where dv.parent = {PARENT};";
            }
        }
    }
}
