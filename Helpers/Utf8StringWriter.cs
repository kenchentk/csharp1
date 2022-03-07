using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Helpers
{
    // https://stackoverflow.com/questions/3871738/force-xdocument-to-write-to-string-with-utf-8-encoding/3871822#3871822
    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
}