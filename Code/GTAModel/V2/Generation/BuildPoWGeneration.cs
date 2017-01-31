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
using System.Collections.Generic;
using Datastructure;
using XTMF;

namespace TMG.GTAModel.V2.Generation
{
    [ModuleInformation( Description
        = "This module is designed to at runtime generate a full set of TMG.GTAModel.V2.PoWGeneration and preload their data."
        + "  This class will then remove itself from the demographic category list."
        + "  Demographic indexes are based on the Durham Model." )]
    public class BuildPoWGeneration : DemographicCategoryGeneration
    {
        [ParentModel]
        public IDemographicCategoyPurpose Parent;

        public override void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            // never gets called
            throw new XTMFRuntimeException( "For '" + this.Name + "' this generate method should never be called!" );
        }

        public override bool RuntimeValidation(ref string error)
        {
            GenerateChildren();
            return true;
        }

        private void AddNewGeneration(List<IDemographicCategoryGeneration> list, int occ, Range age, int employmentStatus, int mobility)
        {
            PowGeneration gen = new PowGeneration();
            gen.Root = this.Root;
            gen.OccupationCategory = CreateRangeSet( occ );
            gen.AgeCategoryRange = CreateRangeSet( age );
            gen.EmploymentStatusCategory = CreateRangeSet( employmentStatus );
            gen.Mobility = CreateRangeSet( mobility );
            gen.ModeChoiceParameterSetIndex = this.ModeChoiceParameterSetIndex;
            gen.DemographicParameterSetIndex = GetDemographicIndex( age.Start, employmentStatus, mobility );
            gen.AllAges = this.AgeCategoryRange;
            list.Add( gen );
        }

        private int ChildAgeIndex(int mobility)
        {
            switch ( mobility )
            {
                case 0:
                    return 0;

                case 1:
                case 3:
                    return 1;

                case 2:
                case 4:
                default:
                    return 2;
            }
        }

        private RangeSet CreateRangeSet(int occ) => new RangeSet(new List<Range> { new Range(occ, occ) });

        private RangeSet CreateRangeSet(Range range) => new RangeSet(new List<Range> { range });

        private void GenerateChildren()
        {
            // we need to generate our children here
            var list = this.Parent.Categories;
            list.Remove( this );
            foreach ( var occSet in this.OccupationCategory )
            {
                for ( var occ = occSet.Start; occ <= occSet.Stop; occ++ )
                {
                    foreach ( var empSet in this.EmploymentStatusCategory )
                    {
                        for ( int employmentStatus = empSet.Start; employmentStatus <= empSet.Stop; employmentStatus++ )
                        {
                            foreach ( var mobilitySet in this.Mobility )
                            {
                                for ( int mobility = mobilitySet.Start; mobility <= mobilitySet.Stop; mobility++ )
                                {
                                    foreach ( var ageSet in this.AgeCategoryRange )
                                    {
                                        AddNewGeneration( list, occ, ageSet, employmentStatus, mobility );
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private int GetDemographicIndex(int age, int employmentStatus, int mobility)
        {
            switch ( age )
            {
                case 0:
                case 1:
                    {
                        return ChildAgeIndex( mobility );
                    }
                case 2:
                    {
                        int empOffset;
                        switch ( employmentStatus )
                        {
                            case 0:
                            case 2:
                                empOffset = 0;
                                break;

                            case 1:
                            default:
                                empOffset = 3;
                                break;
                        }
                        return ChildAgeIndex( mobility ) + 3 + empOffset;
                    }
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                default:
                    int ageOffset = ( ( age < 6 ? age : 6 ) - 3 ) * 12;
                    int employmentOffset = ( employmentStatus == 1 ? 3 : 0 );
                    int mobilityOffset = ( mobility < 3 ? mobility : ( mobility - 3 ) + 7 );
                    return ageOffset + mobilityOffset + employmentOffset + 9;
            }
        }
    }
}