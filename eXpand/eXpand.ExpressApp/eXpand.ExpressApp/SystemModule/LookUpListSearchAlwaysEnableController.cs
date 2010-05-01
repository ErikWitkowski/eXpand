﻿using System.Collections.Generic;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base.General;

namespace eXpand.ExpressApp.SystemModule
{
    public enum LookUpListSearch
    {
        Default,
        AlwaysEnable
    }

    public interface IModelListViewLookUpListSearch
    {
        LookUpListSearch LookUpListSearch { get; set; }
    }

    public class LookUpListSearchAlwaysEnableController : BaseViewController
    {
        public LookUpListSearchAlwaysEnableController() { }

        protected override void OnActivated()
        {
            base.OnActivated();
            if (Frame.Template is ILookupPopupFrameTemplate)
            {
                if (((IModelListViewLookUpListSearch)View.Model).LookUpListSearch == LookUpListSearch.AlwaysEnable)
                    ((ILookupPopupFrameTemplate)Frame.Template).IsSearchEnabled = true;
            }
        }
        
        public override void UpdateModel(IModelApplication applicationModel)
        {
            return;
            base.UpdateModel(applicationModel);
            IEnumerable<string> enumerable = applicationModel.BOModel
                .Where(wrapper => typeof (ICategorizedItem).IsAssignableFrom(wrapper.TypeInfo.Type))
                .Select(wrapper => wrapper.TypeInfo.FullName);

            foreach (var nodeWrapper in applicationModel.Views.Where(
                wrapper => wrapper.Id.EndsWith("_LookupListView") && enumerable.Contains(wrapper.ModelClass.Name)))
                ((IModelListViewLookUpListSearch)View.Model).LookUpListSearch = LookUpListSearch.AlwaysEnable;
        }

        public override void ExtendModelInterfaces(ModelInterfaceExtenders extenders)
        {
            base.ExtendModelInterfaces(extenders);
            extenders.Add<IModelListView, IModelListViewLookUpListSearch>();
        }
    }
}