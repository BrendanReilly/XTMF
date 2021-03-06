﻿
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using XTMF.Editing;
using XTMF.Gui.Models;
using XTMF.Interfaces;

namespace XTMF.Gui.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class RegionDisplayModel: INotifyPropertyChanged
    {
        private IRegionDisplay _model;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<RegionGroupDisplayModel> Groups { get; set; }

        public IRegionDisplay Model
        {
            get => _model;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="region"></param>
        public RegionDisplayModel(IRegionDisplay region)
        {
            Groups = new ObservableCollection<RegionGroupDisplayModel>();
            this._model = region;

            foreach (var group in region.RegionGroups)
            {
                Groups.Add(new RegionGroupDisplayModel(group));
            }
           ((RegionDisplay)region).PropertyChanged += RegionDisplayModel_PropertyChanged;
            ((RegionDisplay)region).RegionGroups.CollectionChanged += RegionGroupsOnCollectionChanged;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionDisplayModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Model));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Groups.Clear();
            foreach (var g in Model.RegionGroups)
            {
                Groups.Add(new RegionGroupDisplayModel(g));
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class RegionGroupDisplayModel : INotifyPropertyChanged
    {
        private IRegionGroup _model;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="group"></param>
        public RegionGroupDisplayModel(IRegionGroup group)
        {
            _model = group;
            Modules = new ObservableCollection<IModelSystemStructure>();
            foreach (var module in group.Modules)
            {
                Modules.Add(module);
               // ((ModelSystemStructureModel)module).PropertyChanged += RegionGroupDisplayModel_PropertyChanged1;
              
            }

            ((RegionGroup)group).ModulesUpdated += OnModulesUpdated;
            ((RegionGroup)group).PropertyChanged += RegionGroupDisplayModel_PropertyChanged;

        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupDisplayModel_PropertyChanged1(object sender, PropertyChangedEventArgs e)
        {
            return;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupDisplayModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.OnPropertyChanged(nameof(Model));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnModulesUpdated(object sender, EventArgs e)
        {
            var group = sender as IRegionGroup;

            Modules.Clear();
            foreach (var module in group.Modules)
            {
                Modules.Add(module);
            }
        }

        public IRegionGroup Model
        {
            get => _model;
        }

        public ObservableCollection<IModelSystemStructure> Modules { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class RegionDisplaysDisplayModel
    {
        private RegionDisplaysModel _model;

        public ObservableCollection<RegionDisplayModel> Regions { get; set; }

        public RegionDisplaysModel Model
        {
            get => _model;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        public RegionDisplaysDisplayModel(RegionDisplaysModel model)
        {
            Regions = new ObservableCollection<RegionDisplayModel>();
            this._model = model;


            model.RegionViewsUpdated += ModelOnRegionViewsUpdated;

            foreach (var region in model.RegionDisplays)
            {
                Regions.Add(new RegionDisplayModel(region));
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelOnRegionViewsUpdated(object sender, RegionViewsUpdateEventArgs e)
        {

            Regions.Clear();
            foreach (var region in Model.RegionDisplays)
            {

                Regions.Add(new RegionDisplayModel(region));
            }
        }


    }



}
