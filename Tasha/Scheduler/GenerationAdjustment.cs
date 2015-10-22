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
using Datastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace Tasha.Scheduler
{

    public class GenerationAdjustment : IModule
    {
        [RunParameter("Distribution ID Range", "0-262", typeof(RangeSet), "The distribution ID's to alter.")]
        public RangeSet DistributionIDs;

        [RunParameter("Planning Districts", "1-46", typeof(RangeSet), "The planning districts to alter.  The home zone is used for comparison.")]
        public RangeSet PlanningDistricts;

        [RunParameter("Factor", 1.0f, "The factor to apply to this modification for generation rates greater than zero.")]
        public float Factor;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}