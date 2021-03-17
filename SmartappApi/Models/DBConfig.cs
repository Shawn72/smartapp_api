using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace SmartappApi.Models
{
    public class DBConfig
    {
        public string ConString = "";
        public string MysqLConnector()
        {
            return ConString = ConfigurationManager.AppSettings["MYSQL_STRING"];
        }
    }
}