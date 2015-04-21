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
using System.IO;
using TMG.Input;
using TMG.Emme;
using Tasha.Common;
using XTMF;


namespace Tasha.Validation.SmartTrackPerformance 
{
    public class DistanceMatrixCalculation : IEmmeTool
    {

        [SubModelInformation(Required = true, Description = "Distance Matrix .CSV name")]
        public FileLocation DistanceMatrix;

        [RunParameter("Scenario Number", 12, "Which scenario number would you like to get the distances from?")]
        public int scenarioNumber;

        private const string _ToolName = "org.emme.Distance";

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;           
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public bool Execute(Controller controller)
        {

            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }

            var args = string.Join(" ", scenarioNumber, "\"" + DistanceMatrix.GetFilePath() + "\"");

            bool emmeRun;
            emmeRun = mc.Run(_ToolName, args);            

            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}