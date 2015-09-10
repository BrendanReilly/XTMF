﻿/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datastructure;
using TMG;
using TMG.Input;
using XTMF;
namespace Tasha.Data
{

    public class ConvertPDDataToZones : IDataSource<SparseArray<float>>
    {

        [RootModule]
        public ITravelDemandModel Root;

        public bool Loaded
        {
            get;set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseArray<float> Data;

        [SubModelInformation(Required = true, Description = "The loader of PD data")]
        public IReadODData<float> Input;

        public SparseArray<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            var zoneSystem = GetZoneSystem();
            var zones = zoneSystem.GetFlatData();
            var data = zoneSystem.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            var pdMap = zones.Select(z => z.PlanningDistrict).ToArray();
            foreach(var entry in Input.Read())
            {
                var pd = entry.O;
                for(int i = 0; i < pdMap.Length; i++)
                {
                    if(pdMap[i] == pd)
                    {
                        flatData[i] = entry.Data;
                    }
                }
            }
            Data = data;
            Loaded = true;
        }

        private SparseArray<IZone> GetZoneSystem()
        {
            var zoneSystem = Root.ZoneSystem;
            if(zoneSystem.Loaded == false)
            {
                zoneSystem.LoadData();
            }
            return zoneSystem.ZoneArray;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
            Data = null;
        }
    }

}