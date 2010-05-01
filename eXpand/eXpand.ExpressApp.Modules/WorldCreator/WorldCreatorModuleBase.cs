﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Core.DictionaryHelpers;
using DevExpress.ExpressApp.NodeWrappers;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using eXpand.ExpressApp.Core;
using eXpand.ExpressApp.WorldCreator.Core;
using eXpand.Persistent.Base.PersistentMetaData;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.CloneObject;
using eXpand.ExpressApp.WorldCreator.Controllers.ListView;

namespace eXpand.ExpressApp.WorldCreator {
    public abstract class WorldCreatorModuleBase:ModuleBase {
        string _connectionString;
        List<Type> _definedModules=new List<Type>();

        public List<Type> DefinedModules{
            get { return _definedModules; }
        }

        public override void Setup(ApplicationModulesManager moduleManager)
        {
            base.Setup(moduleManager);
            if (Application == null||GetPath()== null)
                return;
            
            TypesInfo.Instance.AddTypes(GetAdditionalClasses());
            Application.SettingUp+=ApplicationOnSettingUp;

            SimpleDataLayer simpleDataLayer = XpoMultiDataStoreProxy.GetDataLayer(_connectionString, GetReflectionDictionary(), TypesInfo.Instance.PersistentAssemblyInfoType);
            var unitOfWork = new UnitOfWork(simpleDataLayer);

            AddDynamicModules(moduleManager, unitOfWork);
            Application.SetupComplete += (sender, args) => {
                mergeTypes(unitOfWork);
                Application.ObjectSpaceProvider.CreateUpdatingSession().UpdateSchema();
            };
            var existentTypesMemberCreator = new ExistentTypesMemberCreator();
            existentTypesMemberCreator.CreateMembers(unitOfWork, TypesInfo.Instance);
        }

        ReflectionDictionary GetReflectionDictionary() {
            Type persistentAssemblyInfoType = TypesInfo.Instance.PersistentAssemblyInfoType;
            IEnumerable<Type> types = persistentAssemblyInfoType.Assembly.GetTypes().Where(type => type.Namespace.StartsWith(persistentAssemblyInfoType.Namespace));
            var reflectionDictionary = new ReflectionDictionary();
            foreach (var type in types) {
                reflectionDictionary.QueryClassInfo(type);    
            }
            return reflectionDictionary;
        }

        void ApplicationOnSettingUp(object sender, SetupEventArgs setupEventArgs) {
            CreateDataStore(setupEventArgs);
        }

        void CreateDataStore(SetupEventArgs setupEventArgs) {
            var objectSpaceProvider = setupEventArgs.SetupParameters.ObjectSpaceProvider as IObjectSpaceProvider;
            if (objectSpaceProvider== null)
                throw new NotImplementedException("ObjectSpaceProvider does not implement " + typeof(IObjectSpaceProvider).FullName);
        }

        public void AddDynamicModules(ApplicationModulesManager moduleManager, UnitOfWork unitOfWork){
            Type assemblyInfoType = TypesInfo.Instance.PersistentAssemblyInfoType;
            List<IPersistentAssemblyInfo> persistentAssemblyInfos =
                new XPCollection(unitOfWork, assemblyInfoType).Cast<IPersistentAssemblyInfo>().Where(info => !info.DoNotCompile &&
                    moduleManager.Modules.Where(@base => @base.Name == "Dynamic" + info.Name + "Module").FirstOrDefault() ==null).ToList();
            _definedModules = new CompileEngine().CompileModules(persistentAssemblyInfos,GetPath());
            foreach (var definedModule in _definedModules){
                moduleManager.AddModule(definedModule);
            }
            unitOfWork.CommitChanges();
        }

        public abstract string GetPath();

        void mergeTypes(UnitOfWork unitOfWork){
            IEnumerable<Type> persistentTypes =
                _definedModules.Select(type => type.Assembly).SelectMany(
                    assembly => assembly.GetTypes().Where(type => typeof(IXPSimpleObject).IsAssignableFrom(type)));
            IDbCommand dbCommand =
                ((ISqlDataStore)XpoDefault.GetConnectionProvider(_connectionString, AutoCreateOption.DatabaseAndSchema)).CreateCommand();
            new XpoObjectMerger().MergeTypes(unitOfWork, persistentTypes.ToList(), dbCommand);
        }

        public IEnumerable<Type> GetAdditionalClasses()
        {
            return Application.Modules.SelectMany(@base => @base.AdditionalBusinessClasses);
        }

        public override void UpdateModel(IModelApplication applicationModel)
        {
            base.UpdateModel(applicationModel);
            if (Application != null){
                ShowOwnerForExtendedMembers(applicationModel);
                removeDynamicAssemblyFromImageSources(applicationModel);
                enableCloning(applicationModel);
            }
        }

        void enableCloning(IModelApplication model)
        {
            foreach (var propertyInfo in typeof(ITypesInfo).GetProperties())
            {
                var type = (Type)propertyInfo.GetValue(TypesInfo.Instance, null);
                ((IModelClassClonable)Application.Model.BOModel[type.FullName]).IsClonable = true;
            }
        }


        void ShowOwnerForExtendedMembers(IModelApplication model)
        {
            foreach (IModelListView listViewInfoNodeWrapper in GetListViewInfoNodeWrappers(model))
            {
                IModelColumn columnInfoNodeWrapper = listViewInfoNodeWrapper.Columns["Owner"];
                if (columnInfoNodeWrapper != null) columnInfoNodeWrapper.Index = 2;
            }
        }

        IEnumerable<IModelListView> GetListViewInfoNodeWrappers(IModelApplication model)
        {
            return
                Application.Model.Views.OfType<IModelListView>().Where(
                view => view.ModelClass.TypeInfo.Type == TypesInfo.Instance.ExtendedReferenceMemberInfoType ||
                view.ModelClass.TypeInfo.Type == TypesInfo.Instance.ExtendedCollectionMemberInfoType ||
                view.ModelClass.TypeInfo.Type == TypesInfo.Instance.ExtendedCoreMemberInfoType);
        }

        void removeDynamicAssemblyFromImageSources(IModelApplication model)
        {
            foreach (Type definedModule in DefinedModules)
            {
                string name = new AssemblyName(definedModule.Assembly.FullName + "").Name;
                var source = ((IModelImageSources)Application.Model).OfType<DevExpress.ExpressApp.Model.IModelAssemblyResourceImageSource>().First(s => s.AssemblyName == name);
                ((IModelImageSources)Application.Model).Remove(source);
            }
        }

        public override void Setup(XafApplication application)
        {
            base.Setup(application);
            application.CreateCustomObjectSpaceProvider +=
                (sender, args) => _connectionString = getConnectionStringWithOutThreadSafeDataLayerInitialization(args);
        }

        string getConnectionStringWithOutThreadSafeDataLayerInitialization(CreateCustomObjectSpaceProviderEventArgs args)
        {
            return args.Connection != null ? args.Connection.ConnectionString : args.ConnectionString;
        }
    }
}