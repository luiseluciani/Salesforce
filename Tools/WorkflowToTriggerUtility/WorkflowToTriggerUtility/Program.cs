using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkflowToTriggerUtility
{
    class fieldUpdate
    {
        public string fullName { get; set; }
        public string description { get; set; }
        public string field { get; set; }
        public string formula { get; set; }
        public string name { get; set; }
        public string operation { get; set; }
        public string literalValue { get; set; }
        public string lookupValue { get; set; }
        public string lookupValueType { get; set; }

        public fieldUpdate() { }
    }

    class workflow
    {
        public string fullName { get; set; }
        public string cleanFullName { get; set; }
        public List<fieldUpdate> actions { get; set; }
        public Boolean active { get; set; }
        public List<criteriaItem> criteriaItems { get; set; }
        public string description { get; set; }
        public string triggerType { get; set; }
        public string formula { get; set; }
        public string booleanFilter { get; set; }

        public workflow() { }
    }

    class criteriaItem
    {
        public string field { get; set; }
        public string operation { get; set; }
        public string value { get; set; }

        public criteriaItem() { }
    }

    class Program
    {
        public static string wfMethodTempate =
@"
/*************************************************************
This method has been created from a workflow
Workflow Name: {0}
Workflow Description: {2}
Workflow Type: {3}
*************************************************************/
public static void w2t_{1}(Case oldCase, Case newCase)
{
    Boolean isActive = {4};
    {5}
    if(isActive && condition)
    {
{6}
    }
}

";
        public static Dictionary<string, List<string>> LookupsNeeded { get; set; }
        public static Dictionary<string, fieldUpdate> fieldUpdates { get; set; }
        public static List<workflow> workflows { get; set; }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide a path to a Workflow file");
                return;
            }

            string pathToWFFile = args[0];

            LookupsNeeded = new Dictionary<string, List<string>>();
            List<workflow> workflows = getWorflowRules(pathToWFFile);
            string fileContent = "";
            //Create code for the Lookups needed
            if (LookupsNeeded.Count > 0)
            {
                fileContent += createCodeForLookups();
            }

            List<string> beforeInsert = new List<string>();
            List<string> beforeUpdate = new List<string>();

            foreach(workflow w in workflows)
            {
                fileContent += createCodeFromWorkflow(w);

                if (w.triggerType == "onCreateOnly" || w.triggerType == "onCreateOrTriggeringUpdate" || w.triggerType == "allChanges")
                {
                    beforeInsert.Add("w2t_"+w.cleanFullName);
                }
                if (w.triggerType == "onCreateOrTriggeringUpdate" || w.triggerType == "allChanges")
                {
                    beforeUpdate.Add("w2t_" + w.cleanFullName);
                }
            }

            using (StreamWriter outfile = new StreamWriter(@"CaseTriggerWorkflows.txt"))
            {
                outfile.Write(fileContent);
            }

            using (StreamWriter outfile = new StreamWriter(@"CaseTriggerWorkflowsCaller.txt"))
            {
                outfile.WriteLine("public void beforeInsert(Case newCase)");
                outfile.WriteLine("{");
                foreach (string wcall in beforeInsert)
                {
                    outfile.WriteLine("\t"+wcall+"(null, newCase);");
                }
                outfile.WriteLine("}");
                outfile.WriteLine("");
                outfile.WriteLine("public void beforeUpdate(Case oldCase, Case newCase)");
                outfile.WriteLine("{");
                foreach (string wcall in beforeUpdate)
                {
                    outfile.WriteLine("\t" + wcall + "(oldCase, newCase);");
                }
                outfile.WriteLine("}");
                outfile.WriteLine("");
            }

            Process.Start("CaseTriggerWorkflows.txt");
        }

        private static string createCodeForLookups()
        {
            string result = "";
            foreach (string objectName in LookupsNeeded.Keys)
            {
                if (objectName == "RecordType" || objectName == "Queue") continue;

                result += "public static List<" + objectName + "> lookup_" + objectName+" {get;set;}\n";
            }

            if (result != "")
            {
                result += "\n\n" + "public static initWF2TRLookups()\n{\n";
                foreach (string objectName in LookupsNeeded.Keys)
                {
                    if (objectName == "RecordType" || objectName == "Queue") continue;

                    string LookupVals = "'" + string.Join("','", LookupsNeeded.Values) + "'";
                    result += "\tlookup_" + objectName + " EOUtils.listToMap('Name',[SELECT id, Name FROM " + objectName + " WHERE Name IN (" + LookupVals + ")]);\n";
                }
                result += "}\n\n";
            }
            return result;
        }

        private static string createCodeFromWorkflow(workflow wf)
        {
            string result = wfMethodTempate;
            string bFilter;

            result = result.Replace("{0}", wf.fullName);
            result = result.Replace("{1}", wf.cleanFullName);
            result = result.Replace("{2}", wf.description);
            result = result.Replace("{3}", wf.triggerType);
            result = result.Replace("{4}", wf.active.ToString());

            //createCodeForConditions
            string conditions = "Boolean condition = true;";
            if (wf.formula != null && wf.formula != "")
            {
                conditions = "Boolean condition = false; /* TODO: Boolean condition = " + wf.formula + "; */";
            }
            else
            {
                List<string> conditionPart = new List<string>();
                foreach (criteriaItem c in wf.criteriaItems)
                {
                    conditionPart.Add(opToCode(c.operation,c.field,c.value));
                }

                if (wf.booleanFilter != null && wf.booleanFilter != "")
                {
                    bFilter = wf.booleanFilter.Replace("AND", "&&").Replace("OR", "||");

                    Regex rgx = new Regex(@"\d*");
                    List<string> nums = new List<string>();
                    foreach (Match m in rgx.Matches(bFilter,0))
                    {
                        if(m.Value != "")
                            nums.Add(m.Value);
                    }
                    for (int i = 0; i < nums.Count; i++ )
                    {
                        var regex = new Regex(Regex.Escape(nums[i]));
                        bFilter = regex.Replace(bFilter, "[[" + nums[i] + "]]", 1);
                    }

                    conditions = "Boolean condition = " + bFilter + ";";

                    for (int i = conditionPart.Count -1; i >=0 ; i--)
                    {
                        conditions = conditions.Replace("[["+(i + 1).ToString()+"]]", conditionPart[i]);
                    }
                }
                else
                {
                    conditions = "Boolean condition = "+String.Join(" && ",conditionPart)+";";
                }
            }

            result = result.Replace("{5}", conditions);

            //Create field updates
            string updates = "";
            foreach(fieldUpdate u in wf.actions)
            {
                if (u.operation == "Formula")
                {
                    updates += "\t\t/* TODO: newCase."+u.field+" = "+u.formula+";*/\n";
                }
                else if (u.operation == "Literal")
                {
                    int dum;
                    string litVal = (int.TryParse(u.literalValue,out dum))?u.literalValue:"'"+u.literalValue+"'";
                    updates += "\t\tnewCase." + u.field + " = " + litVal + ";\n";
                }
                else if (u.operation == "LookupValue")
                {
                    if (u.lookupValueType == "RecordType")
                    {
                        updates += "\t\tnewCase." + u.field + " = caseRecordTypesByName.get('" + u.lookupValue + "').getRecordTypeId();\n";
                    }
                    else if (u.lookupValueType == "Queue")
                    {
                        updates += "\t\tnewCase." + u.field + " = queuesMapByName.get('" + u.lookupValue + "').Id;\n";
                    }
                    else if (u.lookupValue == null)
                    {
                        updates += "\t\tnewCase." + u.field + " = null;\n";
                    }
                    else
                    {
                        updates += "\t\tnewCase." + u.field + " = lookup_" + u.lookupValueType + ".get('" + u.lookupValue + "');\n";
                    }
                }
                else if (u.operation == "Null")
                {
                    updates += "\t\tnewCase." + u.field + " = null;\n";
                }
            }

            result = result.Replace("{6}", updates);

            return result;
        }

        private static Dictionary<string, fieldUpdate> getFieldUpdates(string pathToWFFile)
        {
            XmlReader reader = XmlReader.Create(pathToWFFile);
            fieldUpdates = new Dictionary<string, fieldUpdate>();
            while (reader.ReadToFollowing("fieldUpdates"))
            {
                fieldUpdate temp = new fieldUpdate();
                while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "fieldUpdates"))
                {
                    reader.Read();
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string field = reader.Name;
                        string val = reader.ReadElementContentAsString();
                        switch (field)
                        {
                            case "fullName":
                                temp.fullName = val;
                                break;
                            case "description":
                                temp.description = val;
                                break;
                            case "field":
                                temp.field = val;
                                break;
                            case "formula":
                                temp.formula = val;
                                break;
                            case "name":
                                temp.name = val;
                                break;
                            case "operation":
                                temp.operation = val;
                                break;
                            case "literalValue":
                                temp.literalValue = val;
                                break;
                            case "lookupValue":
                                temp.lookupValue = val;
                                break;
                            case "lookupValueType":
                                temp.lookupValueType = val;
                                break;
                        }
                    }
                }
                if (temp.operation == "Lookup")
                {
                    if (!LookupsNeeded.ContainsKey(temp.lookupValueType))
                    {
                        LookupsNeeded[temp.lookupValueType] = new List<string>();
                    }

                    if(!LookupsNeeded[temp.lookupValueType].Contains(temp.lookupValue))
                        LookupsNeeded[temp.lookupValueType].Add(temp.lookupValue);
                }
                fieldUpdates.Add(temp.fullName, temp);
            }
            return fieldUpdates;
        }

        private static List<workflow> getWorflowRules(string pathToWFFile)
        {
            Dictionary<string, fieldUpdate> fieldUpdates = getFieldUpdates(pathToWFFile);
            XmlReader reader = XmlReader.Create(pathToWFFile);
            workflows = new List<workflow>();
            while (reader.ReadToFollowing("rules"))
            {
                workflow temp = new workflow();
                Boolean hasworkflowTimeTriggers = false;
                while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "rules"))
                {
                    reader.Read();
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string elementName = reader.Name;
                        if (elementName == "actions")
                        {
                            string actionName = "", actionType = "";

                            while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "actions"))
                            {
                                reader.Read();
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    string subElementName = reader.Name;
                                    string val = reader.ReadElementContentAsString();
                                    if (subElementName == "name")
                                    {
                                        actionName = val;
                                    }
                                    else if (subElementName == "type")
                                    {
                                        actionType = val;
                                    }
                                }
                            }

                            if (actionType == "FieldUpdate" && fieldUpdates.ContainsKey(actionName))
                            {
                                if (temp.actions == null)
                                    temp.actions = new List<fieldUpdate>();

                                temp.actions.Add(fieldUpdates[actionName]);
                            }

                        }
                        else if (elementName == "criteriaItems")
                        {
                            criteriaItem tempCriteria = new criteriaItem();
                            while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "criteriaItems"))
                            {
                                reader.Read();
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    string subElementName = reader.Name;
                                    string val = reader.ReadElementContentAsString();
                                    if (subElementName == "field")
                                    {
                                        tempCriteria.field = val;
                                    }
                                    else if (subElementName == "operation")
                                    {
                                        tempCriteria.operation = val;
                                    }
                                    else if (subElementName == "value")
                                    {
                                        tempCriteria.value = val;
                                    }
                                }
                            }
                            if (temp.criteriaItems == null)
                                temp.criteriaItems = new List<criteriaItem>();
                            temp.criteriaItems.Add(tempCriteria);
                        }
                        else if(elementName != "workflowTimeTriggers")
                        {
                            string val = reader.ReadElementContentAsString();

                            switch (elementName)
                            {
                                case "fullName":
                                    temp.fullName = Uri.UnescapeDataString(val);
                                    Regex rgx = new Regex(@"\W");
                                    temp.cleanFullName = rgx.Replace(temp.fullName, "");
                                    break;
                                case "description":
                                    temp.description = val;
                                    break;
                                case "active":
                                    temp.active = val == "true";
                                    break;
                                case "formula":
                                    temp.formula = val;
                                    break;
                                case "triggerType":
                                    temp.triggerType = val;
                                    break;
                                case "booleanFilter":
                                    temp.booleanFilter = val;
                                    break;
                            }
                        }
                        else if (elementName == "workflowTimeTriggers")
                        {
                            hasworkflowTimeTriggers = true;
                        }
                    }
                }

                if (temp.actions != null && !hasworkflowTimeTriggers) //We only care about fielUpdate Workflows
                    workflows.Add(temp);
            }
            return workflows;
        }

        private static string opToCode(string op, string op1, string op2)
        {
            op1 = "newCase." + op1.Replace("Case.","");
            int dummy;
            if (!int.TryParse(op2,out dummy))
            {
                op2 = "'" + op2 + "'";
            }
            switch (op)
            {
                case "equals":
                    return op1 + " == "+op2;
                case "notEqual":
                    return op1 + " != " + op2;
                case "startsWith":
                    return op1 + ".startsWith("+op2+")";
                case "contains":
                    return op1 + ".contains(" + op2 + ")";
                case "notContain":
                    return "!"+op1 + ".contain(" + op2 + ")";
                case "lessThan":
                    return op1 + " < " + op2;
                case "greaterThan":
                    return op1 + " > " + op2;
                case "lessOrEqual":
                    return op1 + " <= " + op2;
                case "greaterOrEqual":
                    return op1 + " >= " + op2;
                case "includes":
                    return "includes(" + op1 + ", " + op2 + ")";
                case "excludes":
                    return "excludes(" + op1 + ", " + op2 + ")";
                case "within":
                    return "within(" + op1 + ", " + op2 + ")";
            }
            return "";
        }
    }
}
