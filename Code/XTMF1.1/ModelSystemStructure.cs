/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XTMF
{
    public class ModelSystemStructure : IModelSystemStructure2
    {
        private Type _Type;

        public ModelSystemStructure(IConfiguration config, string name, Type parentFieldType)
            : this(config)
        {
            Name = name;
            Children = null;
            Module = null;
            ParentFieldType = parentFieldType;
        }

        internal ModelSystemStructure(IConfiguration config)
        {
            Configuration = config;
        }

        public ModelSystemStructure()
        {

        }

        public bool IsMetaModule { get; set; }

        public IList<IModelSystemStructure> Children
        {
            get;
            set;
        }

        public bool IsDisabled { get; set; }

        public IConfiguration Configuration { get; set; }

        public string Description { get; set; }

        public bool IsCollection { get; private set; }

        public IModule Module
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public IModuleParameters Parameters
        {
            get;
            set;
        }

        public string ParentFieldName { get; set; }

        public Type ParentFieldType { get; set; }

        public bool Required { get; set; }

        public Type Type
        {
            get
            {
                return _Type;
            }

            set
            {
                if (Children != null)
                {
                    Children.Clear();
                }
                if (value != null)
                {
                    if ((Parameters = Project.LoadDefaultParams(value)) != null)
                    {
                        (Parameters as ModuleParameters).BelongsTo = this;
                        foreach (var p in Parameters)
                        {
                            (p as ModuleParameter).BelongsTo = this;
                        }
                    }
                    bool nullBefore = _Type == null;
                    _Type = value;
                    ModelSystemStructure.GenerateChildren(Configuration, this);
                }
                else
                {
                    Parameters = null;
                    _Type = null;
                }
            }
        }

        public static bool CheckForParent(Type parent, Type t)
        {
            foreach (var field in t.GetFields())
            {
                var attributes = field.GetCustomAttributes(typeof(ParentModel), true);
                if (attributes != null && attributes.Length > 0)
                {
                    if (!field.FieldType.IsAssignableFrom(parent))
                    {
                        return false;
                    }
                }
            }
            foreach (var field in t.GetProperties())
            {
                var attributes = field.GetCustomAttributes(typeof(ParentModel), true);
                if (attributes != null && attributes.Length > 0)
                {
                    if (!field.PropertyType.IsAssignableFrom(parent))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool CheckForRootModel(Type root, Type t)
        {
            // get what module is required
            var rootRequirement = GetRootRequirement(t);
            // if there is no requirement then we are fine
            if (rootRequirement == null)
            {
                return true;
            }
            // if there is a requirement, make sure that we can assign to it properly
            return rootRequirement.IsAssignableFrom(root);
        }

        /// <summary>
        /// Check to see which module can act as the root
        /// </summary>
        /// <param name="start">The starting point to check from</param>
        /// <param name="objective">The structure that we are trying to assign to</param>
        /// <param name="t">The type that we want to insert</param>
        /// <returns>The structure that can act as the root for the given type, and objective structure</returns>
        public static IModelSystemStructure CheckForRootModule(IModelSystemStructure start, IModelSystemStructure objective, Type t)
        {
            IModelSystemStructure result = null;
            var rootRequirement = GetRootRequirement(t);
            if (start == objective)
            {
                if (typeof(IModelSystemTemplate).IsAssignableFrom(t))
                {
                    return start;
                }
                return null;
            }
            else if (rootRequirement == null)
            {
                rootRequirement = typeof(IModelSystemTemplate);
            }
            CheckForRootModule(start, objective, rootRequirement, ref result);
            return result;
        }

        public static void GenerateChildren(IConfiguration config, IModelSystemStructure element)
        {
            if (element == null) return;
            if (element.Type == null) return;

            foreach (var field in element.Type.GetFields())
            {
                IModelSystemStructure child = null;
                if ((child = GenerateChildren(element, field.FieldType, field.GetCustomAttributes(true), config)) != null)
                {
                    // set the name
                    child.ParentFieldName = field.Name;
                    child.Name = CreateModuleName(field.Name);
                    if (element.IsCollection)
                    {
                        child.ParentFieldType = field.FieldType.GetGenericArguments()[0];
                    }
                    else
                    {
                        child.ParentFieldType = field.FieldType;
                    }
                    element.Add(child);
                }
            }
            foreach (var property in element.Type.GetProperties())
            {
                IModelSystemStructure child = null;
                if ((child = GenerateChildren(element, property.PropertyType, property.GetCustomAttributes(true), config)) != null)
                {
                    child.ParentFieldName = property.Name;
                    child.Name = CreateModuleName(property.Name);
                    child.ParentFieldType = property.PropertyType;
                    element.Add(child);
                }
            }
            if (element.Children != null & !element.IsCollection)
            {
                SortChildren(element.Children);
            }
        }

        /// <summary>
        /// Get the parent structure of the objective
        /// </summary>
        /// <param name="topModule"></param>
        /// <param name="objective"></param>
        /// <returns>null if the top and objective are not connected, the parent otherwise</returns>
        public static IModelSystemStructure GetParent(IModelSystemStructure topModule, IModelSystemStructure objective)
        {
            // if we are the module, return ourselves
            if (topModule == objective)
            {
                return topModule;
            }
            // otherwise find which list to go down
            IList<IModelSystemStructure> childrenList = topModule.Children;
            // if there are no children then we are done
            if (childrenList == null)
            {
                return null;
            }
            // search our children to see if their are either the objective or are the ancestors of the objective
            IModelSystemStructure ret = null;
            foreach (var child in childrenList)
            {
                if (child == objective)
                {
                    return topModule;
                }
                else if ((ret = GetParent(child, objective)) != null)
                {
                    break;
                }
            }
            // return what we found
            return ret;
        }

        public static Type GetRootRequirement(Type moduleType)
        {
            if (moduleType != null)
            {
                foreach (var field in moduleType.GetFields())
                {
                    var attributes = field.GetCustomAttributes(typeof(RootModule), true);
                    if (attributes != null && attributes.Length > 0)
                    {
                        return field.FieldType;
                    }
                }
                foreach (var field in moduleType.GetProperties())
                {
                    var attributes = field.GetCustomAttributes(typeof(RootModule), true);
                    if (attributes != null && attributes.Length > 0)
                    {
                        return field.PropertyType;
                    }
                }
            }
            return null;
        }

        public static IModelSystemStructure Load(Stream stream, IConfiguration config)
        {
            ModelSystemStructure root = new ModelSystemStructure(config);
            root.Description = "The Model System Template that the project is based on";
            root.Required = true;
            root.ParentFieldType = typeof(IModelSystemTemplate);
            root.ParentFieldName = "Root";
            XmlDocument doc = new XmlDocument();
            doc.Load(stream);
            LoadRoot(config, root, doc["Root"].ChildNodes);
            return root;
        }

        public static IModelSystemStructure Load(string fileName, IConfiguration config)
        {
            ModelSystemStructure root = new ModelSystemStructure(config);
            root.Description = "The Model System Template that the project is based on";
            root.Required = true;
            root.ParentFieldType = typeof(IModelSystemTemplate);
            root.ParentFieldName = "Root";
            if (!File.Exists(fileName))
            {
                return root;
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            var list = doc["Root"].ChildNodes;
            LoadRoot(config, root, list);
            return root;
        }

        public void Add(string name, Type type)
        {
            if (Children == null)
            {
                Children = new List<IModelSystemStructure>();
            }
            var newChild = new ModelSystemStructure(Configuration, name, ParentFieldType);
            newChild.Type = type;
            Children.Add(newChild);
        }

        public void Add(IModelSystemStructure p)
        {
            if (Children == null)
            {
                Children = new List<IModelSystemStructure>();
            }
            Children.Add(p);
        }

        public IModelSystemStructure Clone()
        {
            ModelSystemStructure cloneUs = new ModelSystemStructure(Configuration);
            cloneUs.Name = Name;
            cloneUs.Description = Description;
            cloneUs.Module = Module;
            cloneUs.IsMetaModule = IsMetaModule;
            if (Parameters != null)
            {
                if ((cloneUs.Parameters = Parameters.Clone()) != null)
                {
                    (cloneUs.Parameters as ModuleParameters).BelongsTo = cloneUs;
                    foreach (var p in cloneUs.Parameters)
                    {
                        (p as ModuleParameter).BelongsTo = cloneUs;
                    }
                }
            }
            cloneUs.Required = Required;
            cloneUs.ParentFieldName = ParentFieldName;
            cloneUs.ParentFieldType = ParentFieldType;
            cloneUs._Type = _Type;
            cloneUs.IsCollection = IsCollection;
            cloneUs.IsDisabled = IsDisabled;
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    cloneUs.Add(child.Clone());
                }
            }
            return cloneUs;
        }

        internal ModelSystemStructure GetRoot(ModelSystemStructure modelSystemRoot)
        {
            return ModelSystemStructure.CheckForRootModule(modelSystemRoot, this, Type) as ModelSystemStructure;
        }

        internal ModelSystemStructure GetParent(ModelSystemStructure realModelSystemStructure)
        {
            return ModelSystemStructure.GetParent(realModelSystemStructure, this) as ModelSystemStructure;
        }

        public IModelSystemStructure CreateCollectionMember(Type newType)
        {
            if (IsCollection)
            {
                return CreateCollectionMember(CreateModuleName(newType.Name), newType);
            }
            return null;
        }

        public IModelSystemStructure CreateCollectionMember(string name, Type newType)
        {
            if (IsCollection)
            {
                if (Children == null)
                {
                    Children = new List<IModelSystemStructure>();
                }
                ModelSystemStructure p = new ModelSystemStructure(Configuration);
                Type innerType = ParentFieldType.IsArray ? ParentFieldType.GetElementType()
                    : ParentFieldType.GetGenericArguments()[0];
                p.Type = newType;
                p.ParentFieldType = innerType;
                p.ParentFieldName = ParentFieldName;
                p.Name = name;
                return p;
            }
            return null;
        }

        /// <summary>
        /// Check to see if a type is valid for a module.
        /// </summary>
        /// <param name="type">The type to check for.</param>
        /// <param name="topLevelModule">The top level module</param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal bool CheckPossibleModule(Type type, ModelSystemStructure topLevelModule, ref string error)
        {
            var rootRequirement = GetRootRequirement(type);
            var parent = GetParent(topLevelModule, this);
            if (IsCollection)
            {
                var arguements = ParentFieldType.IsArray ? ParentFieldType.GetElementType() : ParentFieldType.GetGenericArguments()[0];
                if (!(arguements.IsAssignableFrom(type) && (CheckForParent(parent.Type, type)) && CheckForRootModule(topLevelModule, this, rootRequirement) != null))
                {
                    if (!arguements.IsAssignableFrom(type))
                    {
                        error = "The type is not valid for the collection!";
                    }
                    else if (!CheckForParent(parent.Type, type))
                    {
                        error = "This type requires a different parent type!";
                    }
                    else if (CheckForRootModule(topLevelModule, this, rootRequirement) == null)
                    {
                        error = "There is no root module that can support this type at this position!";
                    }
                    return false;
                }
            }
            else
            {
                if (!(ParentFieldType.IsAssignableFrom(type) && (parent == null || CheckForParent(parent.Type, type))
                        && CheckForRootModule(topLevelModule, this, rootRequirement) != null))
                {
                    if (!ParentFieldType.IsAssignableFrom(type))
                    {
                        error = "This type does not meet the requirements of the parent!";
                    }
                    else if (!(parent == null || CheckForParent(parent.Type, type)))
                    {
                        error = "The type does not support the parent as a valid option!";
                    }
                    else if (CheckForRootModule(topLevelModule, this, rootRequirement) == null)
                    {
                        error = "There is no root module that can support this type at this position!";
                    }
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Get all of the possible modules given the top level structure
        /// </summary>
        /// <param name="topModule"></param>
        /// <returns></returns>
        public List<Type> GetPossibleModules(IModelSystemStructure topModule)
        {
            ConcurrentBag<Type> possibleTypes = new ConcurrentBag<Type>();
            var parent = GetParent(topModule, this);
            if (IsCollection)
            {
                GetPossibleModulesCollection(possibleTypes, parent.Type, topModule);
            }
            else
            {
                GetPossibleModulesChildren(topModule, possibleTypes, parent);
            }
            // return a list of the bag
            return new List<Type>(possibleTypes);
        }

        public void Save(Stream stream)
        {
            XmlTextWriter writer = new XmlTextWriter(stream, Encoding.Unicode);
            writer.Formatting = Formatting.Indented;
            writer.WriteStartDocument();
            writer.WriteStartElement("Root");
            Save(writer);
            writer.WriteEndElement();
            writer.Flush();
        }

        public void Save(XmlWriter writer)
        {
            var typesUsed = GatherAllTypes(this);
            var lookUp = CreateInverseLookupTable(typesUsed);
            SaveTypes(writer, typesUsed);
            Save(writer, this, this, lookUp);
            writer.Flush();
        }

        public void Save(string fileName)
        {
            var dirName = Path.GetDirectoryName(fileName);
            if (!String.IsNullOrWhiteSpace(dirName) && !Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                Save(fs);
            }
        }

        public override string ToString()
        {
            return Name != null ? Name : "No Name";
        }

        public bool Validate(ref string error, IModelSystemStructure parent = null)
        {
            if (Required)
            {
                if (IsCollection)
                {
                    if (Children == null || !Children.Any(c => !(c is ModelSystemStructure) || !((ModelSystemStructure)c).IsDisabled))
                    {
                        error = "The collection '" + Name + "' in module '" + parent.Name + "'requires at least one module for the list!\r\nPlease remove this model system from your project and edit the model system.";
                        return false;
                    }
                }
                else
                {
                    if (Type == null)
                    {
                        error = "In '" + Name + "' a type for a required field is not selected for.\r\nPlease remove this model system from your project and edit the model system.";
                        return false;
                    }
                    if (IsDisabled)
                    {
                        error = "In '" + Name + "' a type for a required field is disabled!\r\nPlease remove this model system from your project and edit the model system.";
                        return false;
                    }
                }
            }

            if (ParentFieldType == null)
            {
                error = "There is an error where a parent's field type was not loaded properly!\nPlease contact the TMG to resolve "
                    + "\r\nError for module '" + Name + "' of type '" + Type.FullName + "'";
                return false;
            }

            if (Type != null && !ParentFieldType.IsAssignableFrom(Type))
            {
                error = String.Format("In {2} the type {0} selected can not be assigned to its parent's field of type {1}!", Type, ParentFieldType, Name);
                return false;
            }

            if (Children != null)
            {
                foreach (var child in Children)
                {
                    if (!child.Validate(ref error, this))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal static ModelSystemStructure Load(XmlNode modelSystemNode, IConfiguration config)
        {
            XTMF.ModelSystemStructure structure = new ModelSystemStructure(config);
            LoadRoot(config, structure, modelSystemNode.ChildNodes);
            return structure;
        }

        private static void AddIfNotContained(Type t, List<Type> ret)
        {
            if (t != null)
            {
                if (!ret.Contains(t))
                {
                    ret.Add(t);
                }
            }
        }

        private static Type AquireTypeFromField(IModelSystemStructure parent, string fieldName)
        {
            if (parent.Type == null)
            {
                return null;
            }
            var field = parent.Type.GetField(fieldName);
            if (field != null)
            {
                return field.FieldType;
            }
            else
            {
                var property = parent.Type.GetProperty(fieldName);
                if (property != null)
                {
                    return property.PropertyType;
                }
            }
            return null;
        }

        private static void AssignTypeValue(XmlAttribute paramTIndex, XmlAttribute paramTypeAttribute, XmlAttribute paramValueAttribute, IModuleParameter selectedParam, Dictionary<int, Type> lookUp)
        {
            string error = null;
            var temp = ArbitraryParameterParser.ArbitraryParameterParse(selectedParam.Type, paramValueAttribute.InnerText, ref error);
            if (temp != null)
            {
                // don't overwrite the default if we are loading something bad
                selectedParam.Value = temp;
            }
        }

        /// <summary>
        /// Recursively find the last instance of a structure that is connected to the objective that
        /// is able to satisfy t's root requirement
        /// </summary>
        /// <param name="start"></param>
        /// <param name="objective"></param>
        /// <param name="rootRequirement"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static bool CheckForRootModule(IModelSystemStructure start, IModelSystemStructure objective, Type rootRequirement, ref IModelSystemStructure result)
        {
            if (start == objective)
            {
                if (result == null && objective.Type != null && rootRequirement.IsAssignableFrom(objective.Type))
                {
                    // if there is no result yet check to see if we can match the root request
                    result = start;
                }
                return true;
            }
            IList<IModelSystemStructure> childrenList = start.Children;
            if (childrenList == null)
            {
                return false;
            }
            foreach (var child in childrenList)
            {
                if (CheckForRootModule(child, objective, rootRequirement, ref result))
                {
                    // check to see if we have a result already
                    if (result == null && start.Type != null && rootRequirement.IsAssignableFrom(start.Type))
                    {
                        // if there is no result yet check to see if we can match the root request
                        result = start;
                    }
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<Type, int> CreateInverseLookupTable(List<Type> typesUsed)
        {
            Dictionary<Type, int> ret = new Dictionary<Type, int>();
            for (int i = 0; i < typesUsed.Count; i++)
            {
                ret[typesUsed[i]] = i;
            }
            return ret;
        }

        private static string CreateModuleName(string baseName)
        {
            StringBuilder nameBuilder = new StringBuilder(50);
            var length = baseName.Length;
            bool lastUpper = true;
            if (length > 0)
            {
                nameBuilder.Append(baseName[0]);
            }
            for (int i = 1; i < length; i++)
            {
                var c = baseName[i];
                if (Char.IsUpper(c))
                {
                    if (!lastUpper)
                    {
                        nameBuilder.Append(' ');
                    }
                }
                else
                {
                    lastUpper = false;
                }
                nameBuilder.Append(c);
            }
            return nameBuilder.ToString();
        }

        private static List<Type> GatherAllTypes(ModelSystemStructure start)
        {
            List<Type> ret = new List<Type>();
            GatherAllTypes(start, ret);
            return ret;
        }

        private static void GatherAllTypes(IModelSystemStructure current, List<Type> ret)
        {
            // Gather the types
            GatherTypes(current, ret);
            // recurse for the rest of the structure
            if (current.Children != null)
            {
                foreach (var child in current.Children)
                {
                    GatherAllTypes(child, ret);
                }
            }
        }

        private static void GatherTypes(IModelSystemStructure current, List<Type> ret)
        {
            AddIfNotContained(current.ParentFieldType, ret);
            AddIfNotContained(current.Type, ret);
            var parameters = current.Parameters;
            if (parameters != null)
            {
                foreach (var v in parameters)
                {
                    AddIfNotContained(v.Type, ret);
                }
            }
        }

        private static IModelSystemStructure GenerateChildren(IModelSystemStructure element, Type type, object[] attributes, IConfiguration config)
        {
            Type iModel = typeof(IModule);
            if (type.IsArray)
            {
                var argument = type.GetElementType();
                if (iModel.IsAssignableFrom(argument))
                {
                    ModelSystemStructure child = new ModelSystemStructure(config);
                    child.IsCollection = true;
                    child.Children = new List<IModelSystemStructure>();
                    foreach (var at in attributes)
                    {
                        if (at is DoNotAutomate)
                        {
                            return null;
                        }
                        else if (at is SubModelInformation)
                        {
                            SubModelInformation info = at as SubModelInformation;
                            child.Description = info.Description;
                            child.Required = info.Required;
                        }
                        if (child.Description == null)
                        {
                            child.Description = "No description available";
                            child.Required = false;
                        }
                    }
                    return child;
                }
            }

            if (type.IsGenericType)
            {
                var arguements = type.GetGenericArguments();
                if (arguements != null && arguements.Length == 1)
                {
                    // if the type of this generic is assignable to IModel..
                    if (iModel.IsAssignableFrom(arguements[0]))
                    {
                        Type iCollection = typeof(ICollection<>).MakeGenericType(arguements[0]);
                        if (iCollection.IsAssignableFrom(type))
                        {
                            ModelSystemStructure child = new ModelSystemStructure(config);
                            child.IsCollection = true;
                            child.Children = new List<IModelSystemStructure>();
                            foreach (var at in attributes)
                            {
                                if (at is DoNotAutomate)
                                {
                                    return null;
                                }
                                else if (at is SubModelInformation)
                                {
                                    SubModelInformation info = at as SubModelInformation;
                                    child.Description = info.Description;
                                    child.Required = info.Required;
                                }
                                if (child.Description == null)
                                {
                                    child.Description = "No description available";
                                    child.Required = false;
                                }
                            }
                            return child;
                        }
                    }
                }
            }

            if (iModel.IsAssignableFrom(type))
            {
                ModelSystemStructure child = new ModelSystemStructure(config);
                foreach (var at in attributes)
                {
                    if (at is ParentModel || at is DoNotAutomate || at is RootModule)
                    {
                        return null;
                    }
                    if (at is SubModelInformation)
                    {
                        SubModelInformation info = at as SubModelInformation;
                        child.Description = info.Description;
                        child.Required = info.Required;
                    }
                }
                if (child.Description == null)
                {
                    child.Description = "No description available";
                    child.Required = false;
                }
                return child;
            }
            return null;
        }

        /// <summary>
        /// Discern the type of the passed XmlNode. Currently either collection or single module type.
        /// This is done to somewhat prevent broken project / model system loading of the underlying DLL definition changes.
        /// </summary>
        /// <param name="parent">The parent ModelSystemStructure element to this passed node.</param>
        /// <param name="currentNode">The node being investigated for its "code" type.</param>
        /// <returns>Returns the Type of the passed node, null if no type could be matched.</returns>
        private static Type DiscernType(IModelSystemStructure parent, string fieldName)
        {

            Type type = parent.Type;
            if (type == null)
            {
                return null;
            }

                System.Reflection.FieldInfo[] fieldsInfo = type.GetFields();
                foreach (var field in fieldsInfo)
                {

                    if (field.Name == fieldName)
                    {
               
                        return field.FieldType;
                    }
                }
            
      

            return null;
        }

        private static void Load(IModelSystemStructure projectStructure, IModelSystemStructure parent, XmlNode currentNode, IConfiguration config, Dictionary<int, Type> lookup)
        {
            var mod = projectStructure as ModelSystemStructure;
            var attributes = currentNode.Attributes;
            if (attributes == null)
            {
                throw new Exception("When loading a module we were unable to get the XML attributes for a module!");
            }
            var nameAttribute = attributes["Name"];
            var descriptionAttribute = attributes["Description"];
            var typeAttribute = attributes["Type"];
            var tIndexAttribute = attributes["TIndex"];
            var parentFieldNameAttribute = attributes["ParentFieldName"];
            var parentFieldTypeAttribute = attributes["ParentFieldType"];
            var parentTIndexAttribute = attributes["ParentTIndex"];
            var isMetaAttribute = attributes["IsMeta"];
            if (mod != null)
            {
                var isDisabled = attributes["IsDisabled"];
                var disabled = false;
                if (isDisabled != null)
                {
                    bool.TryParse(isDisabled.InnerText, out disabled);
                }
                mod.IsDisabled = disabled;
            }
            if (nameAttribute != null)
            {
                projectStructure.Name = nameAttribute.InnerText;
            }
            if (isMetaAttribute != null)
            {
                projectStructure.IsMetaModule = true;
            }
            // Find the type
            if (tIndexAttribute != null)
            {
                int index = -1;
                if (!int.TryParse(tIndexAttribute.InnerText, out index))
                {
                    index = -1;
                }
                if (index >= 0)
                {

                    Type t;
                    if (lookup.TryGetValue(index, out t))
                    {
                        projectStructure.Type = t;
                    }
                    else
                    {
                        projectStructure.Type = null;
                    }
                }
                else
                {
                    projectStructure.Type = null;
                }
            }
            else if (typeAttribute != null)
            {
                string typeName = typeAttribute.InnerText;
                if (typeName == "null")
                {
                    projectStructure.Type = null;
                }
                else
                {
                    projectStructure.Type = Type.GetType(typeName);
                }
            }
            projectStructure.Description = descriptionAttribute != null ? descriptionAttribute.InnerText : GetDefaultDescription(projectStructure, parent);
            if (parentFieldNameAttribute != null)
            {
                projectStructure.ParentFieldName = parentFieldNameAttribute.InnerText;
            }
            if (parentTIndexAttribute != null)
            {
                int index = -1;
                if (!int.TryParse(parentTIndexAttribute.InnerText, out index))
                {
                    index = -1;
                }
                if (index >= 0)
                {
                    projectStructure.ParentFieldType = lookup[index];
                    if (projectStructure.ParentFieldType == null)
                    {
                        projectStructure.ParentFieldType = AquireTypeFromField(parent, projectStructure.ParentFieldName);
                    }
                }
                else
                {
                    projectStructure.ParentFieldType = AquireTypeFromField(parent, projectStructure.ParentFieldName);
                }
            }
            else if (parentFieldTypeAttribute != null)
            {
                var typeName = parentFieldTypeAttribute.InnerText;
                if (typeName == "null")
                {
                    projectStructure.ParentFieldType = AquireTypeFromField(parent, projectStructure.ParentFieldName);
                }
                else
                {
                    projectStructure.ParentFieldType = Type.GetType(typeName);
                }
            }
            // get the default parameters before loading from
            if ((projectStructure.Parameters = Project.LoadDefaultParams(projectStructure.Type)) != null)
            {
                (projectStructure.Parameters as ModuleParameters).BelongsTo = projectStructure;
                foreach (var p in projectStructure.Parameters)
                {
                    (p as ModuleParameter).BelongsTo = projectStructure;
                }
            }
            if (currentNode.HasChildNodes)
            {
                foreach (XmlNode child in currentNode.ChildNodes)
                {
                    LoadChildNode(projectStructure, child, config, lookup);
                }
                //Organize in alphabetical order
                if (!projectStructure.IsCollection & projectStructure.Children != null)
                {
                    SortChildren(projectStructure.Children);
                }
            }
        }

        private static string GetDefaultDescription(IModelSystemStructure projectStructure, IModelSystemStructure parent)
        {
            // we can't get information from collections or if the parent isn't defined
            if (parent == null || parent.IsCollection || projectStructure.ParentFieldName == null)
            {
                return String.Empty;
            }
            // otherwise scan the type to see if we can get any information
            var type = parent.Type;
            var field = type.GetField(projectStructure.ParentFieldName);
            var property = type.GetProperty(projectStructure.ParentFieldName);
            if (field != null)
            {
                foreach (var at in field.GetCustomAttributes(true))
                {
                    var info = at as SubModelInformation;
                    if (info != null)
                    {
                        return info.Description;
                    }
                }
            }
            else if (property != null)
            {
                foreach (var at in property.GetCustomAttributes(true))
                {
                    var info = at as SubModelInformation;
                    if (info != null)
                    {
                        return info.Description;
                    }
                }
            }
            // if we can't find anything the default is none
            return String.Empty;
        }



        /// <summary>
        /// Sort the list of model system structures based upon their name.
        /// </summary>
        /// <param name="list">The list of model system structures to sort.</param>
        private static void SortChildren(IList<IModelSystemStructure> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                bool anyChanges = false;
                for (int j = 0; j < list.Count - 1 - i; j++)
                {
                    if (list[j].Name.CompareTo(list[j + 1].Name) > 0)
                    {
                        var temp = list[j];
                        list[j] = list[j + 1];
                        list[j + 1] = temp;
                        anyChanges = true;
                    }
                }
                if (!anyChanges)
                {
                    break;
                }
            }
        }

        private static bool IsCollectionType(Type t)
        {
            return t.GetInterface("System.Collections.Generic.ICollection`1") != null;
        }

        private static void LoadChildNode(IModelSystemStructure modelSystemStructure, XmlNode child, IConfiguration config, Dictionary<int, Type> lookUp)
        {
            /* Check the parent class type */
            
            Type type = null;
            if (child.Attributes["Name"] != null)
            {
                 type = DiscernType(modelSystemStructure, child.Attributes["Name"].Value);
            }
        
            if(type != null)
            {
                if (typeof(IModule).IsAssignableFrom(type))
                {
                    LoadModule(modelSystemStructure, child, config, lookUp);
                }
                else if (typeof(ICollection<IModule>).IsAssignableFrom(type)
                    || (type.GenericTypeArguments.Length == 1 && typeof(IModule).IsAssignableFrom(type.GenericTypeArguments[0]) && IsCollectionType(type))
                    )
                {
                    LoadCollection(modelSystemStructure, child, config, lookUp);
                }
            }
            else
            {

        
                switch (child.Name)
                {
                    case "Module":
                        {
                            LoadModule(modelSystemStructure, child, config, lookUp);
                        }
                        break;

                    case "Collection":
                        {
                            LoadCollection(modelSystemStructure, child, config, lookUp);
                        }
                        break;

                    case "Parameters":
                        {
                            LoadParameters(modelSystemStructure, child, lookUp);
                        }
                        break;
                }
            }
        }

        private static void LoadCollection(IModelSystemStructure parent, XmlNode child, IConfiguration config, Dictionary<int, Type> lookUp)
        {
            IModelSystemStructure us = null;
            
            var attributes = child.Attributes;
            if (attributes == null)
            {
                throw new Exception("When loading a module we were unable to get the XML attributes for a collection!");
            }
            var paramNameAttribute = attributes["ParentFieldName"];
            var paramTIndexAttribute = attributes["ParentTIndex"];
            var paramTypeAttribute = attributes["ParentFieldType"];
            var nameAttribute = attributes["Name"];
            if (paramNameAttribute != null && (paramTIndexAttribute != null || paramTypeAttribute != null))
            {
                if (parent.Children == null)
                {
                    return;
                }
                for (int i = 0; i < parent.Children.Count; i++)
                {
                    if (parent.Children[i].ParentFieldName == paramNameAttribute.InnerText)
                    {
                        us = parent.Children[i];
                        break;
                    }
                }
                if (us != null)
                {
                    var mod = us as ModelSystemStructure;
                    if (mod != null)
                    {
                        var isDisabled = attributes["IsDisabled"];
                        var disabled = false;
                        if (isDisabled != null)
                        {
                            bool.TryParse(isDisabled.InnerText, out disabled);
                        }
                        mod.IsDisabled = disabled;
                    }
                    us.ParentFieldType = AquireTypeFromField(parent, us.ParentFieldName);
                    if (nameAttribute != null)
                    {
                        us.Name = nameAttribute.InnerText;
                    }
                    us.ParentFieldName = paramNameAttribute.InnerText;
                    // now load the children
                    if (child.HasChildNodes)
                    {
                        foreach (XmlNode element in child.ChildNodes)
                        {
                            XTMF.ModelSystemStructure ps = new ModelSystemStructure(config);
                            Load(ps, us, element, config, lookUp);
                            if (ps.ParentFieldType == null || ps.ParentFieldName == null)
                            {
                                ps.ParentFieldName = us.ParentFieldName;
                                ps.ParentFieldType = us.Type;
                            }
                            us.Children.Add(ps);
                        }
                    }
                }
            }
        }

        private static void LoadDefinitions(XmlNode definitionNode, Dictionary<int, Type> lookUp)
        {
            if (definitionNode.HasChildNodes)
            {
                foreach (XmlNode child in definitionNode.ChildNodes)
                {
                    //writer.WriteStartElement( "Type" );
                    if (child.LocalName == "Type")
                    {
                        try
                        {
                            //writer.WriteElementString( "Name", typesUsed[i].AssemblyQualifiedName );
                            var type = Type.GetType(child.Attributes["Name"].InnerText);
                            int index = -1;
                            //writer.WriteElementString( "TIndex", i.ToString() );
                            if (!int.TryParse(child.Attributes["TIndex"].InnerText, out index))
                            {
                                continue;
                            }
                            lookUp[index] = type;
                        }
                        catch (TypeLoadException)
                        {
                        }
                    }
                }
            }
        }

        private static void LoadModule(IModelSystemStructure modelSystemStructure, XmlNode child, IConfiguration config, Dictionary<int, Type> lookUp)
        {
            if (!modelSystemStructure.IsCollection)
            {
                if (modelSystemStructure.Children != null)
                {
                    var parentFieldNameAttribute = child.Attributes["ParentFieldName"];
                    if (parentFieldNameAttribute != null)
                    {
                        for (int i = 0; i < modelSystemStructure.Children.Count; i++)
                        {
                            if (modelSystemStructure.Children[i].ParentFieldName == parentFieldNameAttribute.InnerText)
                            {
                                Load(modelSystemStructure.Children[i], modelSystemStructure, child, config, lookUp);
                            }
                        }
                    }
                }
            }
            else
            {
                // In the rare case that we are starting with a collection add to the end.
                modelSystemStructure.Children.Add(new ModelSystemStructure(config));
                Load(modelSystemStructure.Children[modelSystemStructure.Children.Count - 1], modelSystemStructure, child, config, lookUp);
            }
        }

        private static void LoadParameters(IModelSystemStructure modelSystemStructure, XmlNode child, Dictionary<int, Type> lookUp)
        {
            if (child.HasChildNodes)
            {
                foreach (XmlNode paramChild in child.ChildNodes)
                {
                    if (paramChild.Name == "Param")
                    {
                        var paramNameAttribute = paramChild.Attributes["Name"];
                        var paramFriendlyNameAttribute = paramChild.Attributes["FriendlyName"];
                        var paramTIndexAttribute = paramChild.Attributes["TIndex"];
                        var paramTypeAttribute = paramChild.Attributes["Type"];
                        var paramValueAttribute = paramChild.Attributes["Value"];
                        var paramQuickParameterAttribute = paramChild.Attributes["QuickParameter"];
                        var paramHiddenAttribute = paramChild.Attributes["Hidden"];
                        if (paramNameAttribute != null || paramTypeAttribute != null || paramValueAttribute != null)
                        {
                            string nameOnModule = paramNameAttribute.InnerText;
                            if (modelSystemStructure.Parameters != null)
                            {
                                ModuleParameter selectedParam = null;
                                foreach (var param in modelSystemStructure.Parameters)
                                {
                                    var p = (ModuleParameter)param;
                                    if (p.NameOnModule == nameOnModule)
                                    {
                                        selectedParam = p;
                                        break;
                                    }
                                }

                                // we will just ignore parameters that no longer exist
                                if (selectedParam != null)
                                {
                                    if(paramHiddenAttribute != null)
                                    {
                                        selectedParam.IsHidden = true;
                                    }
                                    if (paramFriendlyNameAttribute != null)
                                    {
                                        string error = null;
                                        selectedParam.SetName(paramFriendlyNameAttribute.InnerText, ref error);
                                    }
                                    if (paramQuickParameterAttribute != null)
                                    {
                                        bool quick;
                                        if (bool.TryParse(paramQuickParameterAttribute.InnerText, out quick))
                                        {
                                            selectedParam.QuickParameter = quick;
                                        }
                                    }
                                    else
                                    {
                                        selectedParam.QuickParameter = false;
                                    }
                                    AssignTypeValue(paramTIndexAttribute, paramTypeAttribute, paramValueAttribute, selectedParam, lookUp);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void LoadRoot(IConfiguration config, ModelSystemStructure root, XmlNodeList list)
        {

            if (list != null)
            {
                var lookUp = new Dictionary<int, Type>(20);
                for (int i = 0; i < list.Count; i++)
                {
                    var child = list[i];
                    if (child.LocalName == "TypeDefinitions")
                    {
                        LoadDefinitions(child, lookUp);
                    }
                }
                for (int i = 0; i < list.Count; i++)
                {
                    var child = list[i];
                    if (child.LocalName == "Module")
                    {
                        Load(root, null, list[i], config, lookUp);
                    }
                    else if (child.LocalName == "Collection")
                    {
                        root.IsCollection = true;
                        root.Children = new List<IModelSystemStructure>();
                        Load(root, null, list[i], config, lookUp);
                    }
                }
            }
        }

        private static void SaveTypes(XmlWriter writer, List<Type> typesUsed)
        {
            writer.WriteStartElement("TypeDefinitions");
            for (int i = 0; i < typesUsed.Count; i++)
            {
                writer.WriteStartElement("Type");
                writer.WriteAttributeString("Name", typesUsed[i].AssemblyQualifiedName);
                writer.WriteAttributeString("TIndex", i.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        public bool MapGenericsFromTypeToParentType(Type parent, Type t, out Type mappedType)
        {
            var tArguments = t.GetGenericArguments();
            // initially the mapped type is the regular type
            mappedType = t;
            if (!parent.IsGenericType)
            {
                return true;
            }
            Type highestInterface;
            // if the previous is the interface the highest level form is going to be the interface itself, so find it
            if (parent.IsInterface)
            {
                // get the type of the interface that is being bound to
                var toFind = parent.GetGenericTypeDefinition();
                if ((highestInterface = t.GetInterfaces().FirstOrDefault(i => CheckIfTypesAreGenericallyTheSame(i, toFind))) == null)
                {
                    return false;
                }
            }
            else
            {
                // get the parent type of this class that is being bound to
                highestInterface = t;
                while (highestInterface != null && !CheckIfTypesAreGenericallyTheSame(highestInterface, parent))
                {
                    highestInterface = highestInterface.BaseType;
                }
                if (highestInterface == null)
                {
                    return false;
                }
            }
            var originalParentTypes = parent.GetGenericArguments();
            if (originalParentTypes != null)
            {
                //check to make sure the highest order match
                var highestInterfaceTypes = highestInterface.GetGenericArguments();
                for (int i = 0; i < highestInterfaceTypes.Length; i++)
                {
                    if ((!originalParentTypes[i].IsGenericParameter)
                        && (!highestInterfaceTypes[i].IsGenericParameter)
                        && originalParentTypes[i] != highestInterfaceTypes[i])
                    {
                        return false;
                    }
                }
                var map = new Type[tArguments.Length];
                // fill in the generic types with their starting generic values
                for (int i = 0; i < tArguments.Length; i++)
                {
                    map[i] = tArguments[i];
                }

                for (int i = 0; i < highestInterfaceTypes.Length; i++)
                {
                    // if the parent's parameter has been set
                    if (originalParentTypes[i] != highestInterfaceTypes[i])
                    {
                        // go through and find the right type to replace
                        for (int j = 0; j < tArguments.Length; j++)
                        {
                            // and map it
                            if (tArguments[j] == highestInterfaceTypes[i])
                            {
                                map[j] = originalParentTypes[i];
                                break;
                            }
                        }
                    }
                }
                mappedType = t.MakeGenericType(map);
            }
            return true;
        }

        private bool CheckIfTypesAreGenericallyTheSame(Type first, Type second)
        {
            return (first.IsGenericType ? first.GetGenericTypeDefinition() : first) == (second.IsGenericType ? second.GetGenericTypeDefinition() : second);
        }

        private void GetPossibleModulesChildren(IModelSystemStructure topModule, ConcurrentBag<Type> possibleTypes, IModelSystemStructure parent)
        {
            var modules = Configuration.ModelRepository.Modules;
            if (ParentFieldType == null) return;
            Type generalParentForm = ParentFieldType.IsConstructedGenericType ? ParentFieldType.GetGenericTypeDefinition() : null;
            Type[] parentGenericTypes = ParentFieldType.IsConstructedGenericType ? ParentFieldType.GetGenericArguments() : null;
            Parallel.For(0, modules.Count, delegate (int i)
            {
                Type t = modules[i];
                if (t.IsGenericType && !t.IsConstructedGenericType)
                {
                    if (!MapGenericsFromTypeToParentType(ParentFieldType, t, out t))
                    {
                        // if the type is not acceptable just return
                        return;
                    }
                }
                if (ParentFieldType.IsAssignableFrom(t))
                {
                    if ((parent == null || CheckForParent(parent.Type, t))
                        && (CheckForRootModule(topModule, this, t) != null))
                    {
                        possibleTypes.Add(t);
                    }
                }
            })
            ;
        }

        private void GetPossibleModulesCollection(ConcurrentBag<Type> possibleTypes, Type parent, IModelSystemStructure topModule)
        {
            if (ParentFieldType == null) return;

            int count = ParentFieldType.GetGenericArguments().Count();
            var innerCollectionType = ParentFieldType.IsArray ? ParentFieldType.GetElementType() : ParentFieldType.GetGenericArguments()[0];
            var modules = Configuration.ModelRepository.Modules;
            Parallel.For(0, modules.Count, delegate (int i)
            {
                Type t = modules[i];
                if (t.IsGenericType && !t.IsConstructedGenericType)
                {
                    if (!MapGenericsFromTypeToParentType(innerCollectionType, t, out t))
                    {
                        // if the type is not acceptable just return
                        return;
                    }
                }
                if (innerCollectionType.IsAssignableFrom(t)
                    && (CheckForParent(parent, t))
                    && (CheckForRootModule(topModule, this, t) != null))
                {
                    possibleTypes.Add(t);
                }
            });
        }

        private void Save(XmlWriter writer, IModelSystemStructure s, IModelSystemStructure parent, Dictionary<Type, int> lookup)
        {
            if (s.IsCollection)
            {
                SaveCollection(writer, s, parent, lookup);
            }
            else
            {
                SaveModel(writer, s, parent, lookup);
            }
        }

        private void SaveCollection(XmlWriter writer, IModelSystemStructure s, IModelSystemStructure parent, Dictionary<Type, int> lookup)
        {
            var mod = s as ModelSystemStructure;
            writer.WriteStartElement("Collection");
            if (s.ParentFieldType == null)
            {
                throw new XTMFRuntimeException("The type for " + s.Name + "'s Parent was not found!");
            }
            writer.WriteAttributeString("ParentTIndex", lookup[s.ParentFieldType].ToString());
            writer.WriteAttributeString("ParentFieldName", s.ParentFieldName);
            writer.WriteAttributeString("Name", s.Name);
            if (mod != null && mod.IsDisabled)
            {
                writer.WriteAttributeString("Disabled", "true");
            }
            if (s.Children != null)
            {
                foreach (var model in s.Children)
                {
                    Save(writer, model, this, lookup);
                }
            }
            writer.WriteEndElement();
        }

        private void SaveModel(XmlWriter writer, IModelSystemStructure s, IModelSystemStructure parent, Dictionary<Type, int> lookup)
        {
            var mod = s as ModelSystemStructure;
            writer.WriteStartElement("Module");
            writer.WriteAttributeString("Name", s.Name);
            if (GetDefaultDescription(s, parent) != s.Description)
            {
                writer.WriteAttributeString("Description", s.Description);
            }
            if (s.Type == null)
            {
                writer.WriteAttributeString("TIndex", "-1");
            }
            else
            {
                writer.WriteAttributeString("TIndex", lookup[s.Type].ToString());
            }
            if (s.ParentFieldType == null)
            {
                writer.WriteAttributeString("ParentTIndex", "-1");
            }
            else
            {
                writer.WriteAttributeString("ParentTIndex", lookup[s.ParentFieldType].ToString());
            }
            writer.WriteAttributeString("ParentFieldName", s.ParentFieldName);
            if (s.IsMetaModule)
            {
                writer.WriteAttributeString("IsMeta", "true");
            }
            if (mod != null && mod.IsDisabled)
            {
                writer.WriteAttributeString("Disabled", "true");
            }
            SaveParameters(writer, s, lookup);
            if (s.Children != null)
            {
                foreach (var c in s.Children)
                {
                    Save(writer, c, this, lookup);
                }
            }
            writer.WriteEndElement();
        }

        private static void SaveParameters(XmlWriter writer, IModelSystemStructure current, Dictionary<Type, int> lookup)
        {
            // make sure we are loaded before trying to save
            writer.WriteStartElement("Parameters");
            if (current.Parameters != null)
            {
                foreach (var param in current.Parameters)
                {
                    var p = (ModuleParameter)param;
                    writer.WriteStartElement("Param");
                    writer.WriteAttributeString("Name", p.NameOnModule);
                    if (p.Name != p.NameOnModule)
                    {
                        writer.WriteAttributeString("FriendlyName", p.Name);
                    }
                    writer.WriteAttributeString("TIndex", lookup[param.Type == null ? param.Value.GetType() : param.Type].ToString());
                    writer.WriteAttributeString("Value", param.Value == null ? String.Empty : param.Value.ToString());
                    if (param.QuickParameter)
                    {
                        writer.WriteAttributeString("QuickParameter", "true");
                    }
                    if(p.IsHidden)
                    {
                        writer.WriteAttributeString("Hidden", "true");
                    }
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        public List<IModuleMetaProperty> ModuleMetaProperties { get; } = new List<IModuleMetaProperty>();
    }
}