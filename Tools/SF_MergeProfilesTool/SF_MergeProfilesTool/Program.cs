using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace SF_MergeProfilesTool
{
    class Program
    {
        //Will be needing a dictionary for each section of the profile metadata
        public static Dictionary<string,Dictionary<string, Dictionary<string, string>>> profileMetadata { get; set; }

        public static Dictionary<string, string> keyFieldsPerElementType = new Dictionary<string, string> {

            {"applicationVisibilities", "application"},
            {"classAccesses","apexClass"},
            {"externalDataSourceAccesses","externalDataSource"},
            {"fieldLevelSecurities","field"},
            {"fieldPermissions","field"},
            //{"layoutAssignments","layout"},
            {"objectPermissions","object"},
            {"pageAccesses","apexPage"},
            {"recordTypeVisibilities","recordType"},
            {"tabVisibilities","tab"},
            {"userPermissions","name"}
                
        };

        public static Dictionary<string, List<string>> fieldsToMergePerElementType = new Dictionary<string, List<string>> {

            {"applicationVisibilities", new List<string> { "default", "visible" } },
            {"classAccesses", new List<string> { "enabled" } },
            {"externalDataSourceAccesses", new List<string> { "enabled" } },
            {"fieldLevelSecurities", new List<string> { "editable" , "hidden" , "readable" } },
            {"fieldPermissions", new List<string> { "editable" , "hidden" , "readable" } },
            //{"layoutAssignments", new List<string> {} },
            {"objectPermissions", new List<string> { "allowCreate", "allowDelete", "allowEdit", "allowRead", "modifyAllRecords", "viewAllRecords" } },
            {"pageAccesses", new List<string> { "enabled" } },
            {"recordTypeVisibilities", new List<string> {"personAccountDefault", "visible" } },
            {"tabVisibilities", new List<string> { "visibility" } },
            {"userPermissions", new List<string> { "enabled" } }
                
        };

        static void Main(string[] args)
        {

            if (args.Length < 2)
            {
                Console.WriteLine("Please provide 2 profile metadata files to merge");
                return;
            }

            string profile1File = args[0];
            string profile2File = args[1];

            profileMetadata = new Dictionary<string,Dictionary<string, Dictionary<string, string>>>();
            string fileContent = 
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Profile xmlns=""http://soap.sforce.com/2006/04/metadata"">"+"\n";

            parseProfile(profile1File);
            parseProfile(profile2File);


            foreach (string nodeName in profileMetadata.Keys)
            {
                foreach(Dictionary<string, string> element in profileMetadata[nodeName].Values)
                {
                    fileContent += "\t<" + nodeName + ">\n";
                    foreach (string elementName in element.Keys)
                    {
                        fileContent += "\t\t<" + elementName + ">" + element[elementName] + "</" + elementName + ">\n";
                    }
                    fileContent += "\t</" + nodeName + ">\n";
                }
            }

            fileContent += "\t<userLicense>Salesforce</userLicense>\n";
            //fileContent += "\t<fullName>MergedProfile</fullName>\n";
            fileContent += "\t<custom>true</custom>\n";
            //fileContent += "\t<description>This profile is the result of 2 merged profiles: " + Path.GetFileNameWithoutExtension(profile1File) + " and " + Path.GetFileNameWithoutExtension(profile2File) + ".</description>\n";
            fileContent += @"</Profile>";


            using (StreamWriter outfile = new StreamWriter(@"MergedProfile.profile"))
            {
                outfile.Write(fileContent);
            }
        }

        private static void parseProfile(string profileFile)
        {
            XmlReader reader = XmlReader.Create(profileFile);
            string currentlyReading;
            while (reader.Read())
            {
                currentlyReading = "";
                Dictionary<string, string> temp = new Dictionary<string, string>();
                if (reader.Name == "applicationVisibilities" || reader.Name == "classAccesses" || reader.Name == "externalDataSourceAccesses" || reader.Name == "fieldLevelSecurities" || 
                    reader.Name == "fieldPermissions" || reader.Name == "layoutAssignments" || reader.Name == "loginHours" || reader.Name == "loginIpRanges" || reader.Name == "objectPermissions" || 
                    reader.Name == "pageAccesses" || reader.Name == "recordTypeVisibilities" || reader.Name == "tabVisibilities" || reader.Name == "userPermissions")
                {
                    currentlyReading = reader.Name;
                    while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == currentlyReading))
                    {
                        reader.Read();
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            string field = reader.Name;
                            string val = reader.ReadElementContentAsString();
                            temp.Add(field, val);
                        }
                    }

                    if (!profileMetadata.ContainsKey(currentlyReading))
                    {
                        profileMetadata.Add(currentlyReading, new Dictionary<string, Dictionary<string, string>>());
                    }

                    string keyField = keyFieldsPerElementType[currentlyReading];
                    if (profileMetadata[currentlyReading].ContainsKey(temp[keyField]))
                    {
                        List<string> fieldsToMerge = fieldsToMergePerElementType[currentlyReading];
                        mergeFields(profileMetadata[currentlyReading], temp, fieldsToMerge, keyField);
                    }
                    else
                    {
                        profileMetadata[currentlyReading].Add(temp[keyField], temp);
                    }
                }
            }
        }


        private static void mergeFields(Dictionary<string, Dictionary<string, string>> currentData, Dictionary<string, string> newData, List<string> fieldsToMerge, string keyField)
        {
            foreach (string f in fieldsToMerge)
            {
                if (!newData.ContainsKey(f))
                    continue;

                if (!currentData[newData[keyField]].ContainsKey(f))
                {
                    currentData[newData[keyField]] = newData;
                    continue;
                }
                    
                string prevVal = currentData[newData[keyField]][f];
                string curVal = newData[f];

                if (f == "visibility")
                {
                    if ((prevVal == "Hidden" && (curVal == "DefaultOff" || curVal == "DefaultOn")) ||
                        (prevVal == "DefaultOff" && curVal == "DefaultOn"))
                    {
                        currentData[newData[keyField]] = newData;
                    }
                }
                else
                {
                    if (prevVal == "false" && curVal == "true")
                    {
                        currentData[newData[keyField]] = newData;
                    }
                }
            }
        }
    }
}
