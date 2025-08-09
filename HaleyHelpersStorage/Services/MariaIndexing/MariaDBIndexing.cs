using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using static Haley.Internal.IndexingConstant;
using static Haley.Internal.IndexingQueries;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Haley.Utils {
    public partial class MariaDBIndexing : IDSSIndexing {
        const string DB_CORE_SQL_FILE = "dsscore.sql";
        const string DB_CLIENT_SQL_FILE = "dssclient.sql";
        const string DB_CORE_FALLBACK_NAME = "mss_core";
        const string DB_CORE_SEARCH_TERM = "dss_core";
        const string DB_CLIENT_SEARCH_TERM = "dss_client";
        const string DB_SQL_FILE_LOCATION = "Resources";
        public const string DB_MODULE_NAME_PREFIX = "dssm_";
        ILogger _logger;
        string _key;
        IAdapterGateway _agw;
        bool isValidated = false;
        public bool ThrowExceptions { get; set; }
        public MariaDBIndexing(IAdapterGateway agw, string key, ILogger logger) : this(agw, key, logger, false) { }
        public MariaDBIndexing(IAdapterGateway agw, string key, ILogger logger, bool throwExceptions) {
            _key = key;
            _agw = agw;
            _logger = logger;
            ThrowExceptions = throwExceptions;
        }
    }
}
