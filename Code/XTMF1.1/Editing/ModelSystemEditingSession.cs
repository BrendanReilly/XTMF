﻿/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.ComponentModel;
using System.Linq;
using System.Text;
using XTMF.Editing;

namespace XTMF
{
    public sealed class ModelSystemEditingSession : IDisposable
    {
        /// <summary>
        /// The command stack that contains commands that have been done will go on
        /// </summary>
        private EditingStack UndoStack = new EditingStack(100);

        /// <summary>
        /// Get the available types for the selected module
        /// </summary>
        /// <param name="selectedModule">The module to find the valid types for</param>
        /// <returns>A list of the allowed types.</returns>
        public List<Type> GetValidModules(ModelSystemStructureModel selectedModule)
        {
            return selectedModule.RealModelSystemStructure.GetPossibleModules(ModelSystemModel.Root.RealModelSystemStructure);
        }

        /// <summary>
        /// The command stack that commands that have been undone will go on
        /// </summary>
        private EditingStack RedoStack = new EditingStack(100);

        /// <summary>
        /// This event fires when the project containing this model system
        /// was saved externally
        /// </summary>
        public event EventHandler ProjectWasExternallySaved;

        internal void ProjectWasExternalSaved()
        {
            // we need to reload first so the GUI knows how to rebuild the display model.
            Reload();
            var e = ProjectWasExternallySaved;
            if (e != null)
            {
                e(this, new EventArgs());
            }
        }

        /// <summary>
        /// The runtime we are in
        /// </summary>
        private XTMFRuntime Runtime;
        private ModelSystem ModelSystem;
        private int ModelSystemIndex;

        public ProjectEditingSession ProjectEditingSession { get; private set; }

        /// <summary>
        /// This event occurs when no references are left to this editing session.
        /// </summary>
        public event EventHandler SessionClosed;

        private object SessionLock = new object();

        private List<XTMFRun> _Run = new List<XTMFRun>();

        private volatile bool _IsRunning;

        public bool IsRunning
        {
            get { return _IsRunning; }
        }

        /// <summary>
        /// Create a new session to edit a model system
        /// </summary>
        /// <param name="modelSystem">The model system to edit</param>
        public ModelSystemEditingSession(XTMFRuntime runtime, ModelSystem modelSystem)
        {
            Runtime = runtime;
            ModelSystem = modelSystem;
            ModelSystemModel = new ModelSystemModel(this, modelSystem);
            ModelSystemModel.PropertyChanged += ModelSystemModel_PropertyChanged;
        }

        private void ModelSystemModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == ModelSystemModel && e.PropertyName == "Name")
            {
                var changed = NameChanged;
                if (changed != null)
                {
                    changed(this, e);
                }
            }
        }

        /// <summary>
        /// Create a new editing session to edit a project's model system
        /// </summary>
        /// <param name="ProjectEditingSession">The editing session to read from</param>
        /// <param name="modelSystemIndex">The model system's index inside of the project</param>
        public ModelSystemEditingSession(XTMFRuntime runtime, ProjectEditingSession ProjectEditingSession, int modelSystemIndex)
        {
            Runtime = runtime;
            this.ProjectEditingSession = ProjectEditingSession;
            ModelSystemIndex = modelSystemIndex;
            Reload();
        }

        /// <summary>
        /// Rebuild the model system session by recreating the model system model
        /// </summary>
        private void Reload()
        {
            if (ModelSystemModel != null)
            {
                ModelSystemModel.PropertyChanged -= ModelSystemModel_PropertyChanged;
                RedoStack.Clear();
                UndoStack.Clear();
                HasChanged = false;
            }
            ModelSystemModel = new ModelSystemModel(this, this.ProjectEditingSession.Project, ModelSystemIndex);
            ModelSystemModel.PropertyChanged += ModelSystemModel_PropertyChanged;
        }

        internal bool IsEditing(IModelSystemStructure root)
        {
            return (EditingProject && this.ProjectEditingSession.Project.ModelSystemStructure[ModelSystemIndex] == root);
        }

        /// <summary>
        /// Create a model editing session for a previous run.  This will be read-only!
        /// </summary>
        /// <param name="runtime">A link to the XTMFRuntime</param>
        /// <param name="projectSession">The project this is for.</param>
        /// <param name="runFile">The location of the previous run.</param>
        public ModelSystemEditingSession(XTMFRuntime runtime, ProjectEditingSession projectSession, string runFile)
        {
            this.Runtime = runtime;
            ProjectEditingSession = projectSession;
            ModelSystemIndex = -1;
            ModelSystemModel = new ModelSystemModel(Runtime, this, projectSession.Project, runFile);
        }


        /// <summary>
        /// Lets you know if the model system is able to run.
        /// </summary>
        /// <returns>If you can run this model system.</returns>
        public bool CanRun
        {
            get { return this.EditingProject; }
        }

        /// <summary>
        /// Run the model if it is contained inside of a project.
        /// </summary>
        /// <param name="runName"></param>
        /// <param name="error">A message in case of error</param>
        /// <returns></returns>
        public XTMFRun Run(string runName, ref string error)
        {
            // this needs to block as if a command is running
            lock (SessionLock)
            {
                if (!CanRun)
                {
                    error = "You can not run this model system.";
                    return null;
                }
                XTMFRun run;
                if (ModelSystemIndex >= 0)
                {
                    Project cloneProject = ProjectEditingSession.CreateCloneProject(ProjectEditingSession.Project);
                    cloneProject.ModelSystemStructure[ModelSystemIndex] = ModelSystemModel.Root.RealModelSystemStructure;
                    cloneProject.ModelSystemDescriptions[ModelSystemIndex] = ModelSystemModel.Description;
                    cloneProject.LinkedParameters[ModelSystemIndex] = ModelSystemModel.LinkedParameters.GetRealLinkedParameters();
                    run = new XTMFRun(cloneProject, ModelSystemIndex, ModelSystemModel, Runtime.Configuration, runName);
                }
                else
                {
                    run = new XTMFRun(ProjectEditingSession.Project, ModelSystemModel.Root, Runtime.Configuration, runName);
                }
                this._Run.Add(run);
                _IsRunning = true;
                run.RunComplete += () => TerminateRun(run);
                run.ValidationError += (e) => TerminateRun(run);
                run.RuntimeValidationError += (e) => TerminateRun(run);
                return run;
            }
        }

        internal bool SaveAsModelSystem(string name, ref string error)
        {
            lock (SessionLock)
            {
                List<ILinkedParameter> lp;
                var ms = Runtime.ModelSystemController.LoadOrCreate(name);
                ms.Name = name;
                ms.Description = ModelSystemModel.Description;
                if (EditingProject)
                {
                    ms.ModelSystemStructure = ProjectEditingSession.CloneModelSystemStructure(out lp, ModelSystemIndex);
                    ms.LinkedParameters = lp;
                    return ms.Save(ref error);
                }
                else
                {
                    ms.ModelSystemStructure = ModelSystemModel.Root.RealModelSystemStructure;
                    ms.LinkedParameters = ModelSystemModel.LinkedParameters.LinkedParameters.Select(lpm => (ILinkedParameter)lpm.RealLinkedParameter).ToList();
                    using (var otherSession = Runtime.ModelSystemController.EditModelSystem(ms))
                    {
                        return otherSession.Save(ref error);
                    }
                }
            }
        }

        private void TerminateRun(XTMFRun run)
        {
            lock (SessionLock)
            {
                _Run.Remove(run);
                if (_Run.Count == 0)
                {
                    _IsRunning = false;
                }
            }
        }


        /// <summary>
        /// Is the session editing a model system inside of a project?
        /// </summary>
        /// <returns>If it is a project, true.</returns>
        public bool EditingProject
        {
            get
            {
                return ProjectEditingSession != null;
            }
        }

        /// <summary>
        /// Is the session editing a model system that is not in a project?
        /// </summary>
        /// <returns>If it is not a project, true.</returns>
        public bool EditingModelSystem
        {
            get
            {
                return ModelSystem != null;
            }
        }

        /// <summary>
        /// The model system's model that we will interact with.
        /// </summary>
        public ModelSystemModel ModelSystemModel
        {
            get; private set;
        }
        public IConfiguration Configuration
        {
            get
            {
                return Runtime.Configuration;
            }
        }

        /// <summary>
        /// Contains if the model system has changed since the last save.
        /// </summary>
        public bool HasChanged { get; set; }

        /// <summary>
        /// This will return true if closing a window will terminate this session.
        /// </summary>
        public bool CloseWillTerminate
        {
            get
            {
                lock (SessionLock)
                {
                    if (this.EditingModelSystem)
                    {
                        return Runtime.ModelSystemController.WillCloseTerminate(this);
                    }
                    else
                    {
                        return this.ProjectEditingSession.WillCloseTerminate(this.ModelSystemIndex);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler NameChanged;



        /// <summary>
        /// Save the session.
        /// </summary>
        /// <param name="error">If there is an error it will provide a message as to why.</param>
        /// <returns>If we were able to save or not.</returns>
        public bool Save(ref string error)
        {
            lock (SessionLock)
            {
                if (!ModelSystemModel.Save(ref error))
                {
                    return false;
                }
                HasChanged = false;
                return true;
            }
        }

        public bool RunCommand(XTMFCommand command, ref string error)
        {
            lock (SessionLock)
            {
                if (_IsRunning)
                {
                    error = "You can not edit a model system while it is running.";
                    return false;
                }
                if (command.Do(ref error))
                {
                    HasChanged = true;
                    if (command.CanUndo())
                    {
                        UndoStack.Add(command);
                    }
                    // if we do something new, redo no long is available
                    RedoStack.Clear();
                    return true;
                }
                return false;
            }
        }

        public string Name
        {
            get
            {
                return ModelSystemModel.Name;
            }
        }

        public bool SaveAs(string modelSystemName, ref string error)
        {
            lock (SessionLock)
            {
                return ProjectEditingSession.CloneModelSystemAs(Runtime, ModelSystemModel.Root.RealModelSystemStructure,
                    ModelSystemModel.LinkedParameters.GetRealLinkedParameters(),
                    ModelSystemModel.Description, modelSystemName, ref error);
            }
        }

        /// <summary>
        /// Undo the last command
        /// </summary>
        /// <param name="error">The reason it failed</param>
        public bool Undo(ref string error)
        {
            lock (SessionLock)
            {
                XTMFCommand command;
                if (UndoStack.TryPop(out command))
                {
                    if (command != null)
                    {
                        if (command.Undo(ref error))
                        {
                            HasChanged = true;
                            RedoStack.Add(command);
                            return true;
                        }
                        return false;
                    }
                }
                error = "There was nothing to undo.";
                return false;
            }
        }

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        /// <param name="error">The reason why it failed</param>
        public bool Redo(ref string error)
        {
            lock (SessionLock)
            {
                XTMFCommand command;
                if (RedoStack.TryPop(out command))
                {
                    if (command != null)
                    {
                        if (command.Redo(ref error))
                        {
                            HasChanged = true;
                            UndoStack.Add(command);
                            return true;
                        }
                        return false;
                    }
                }
                error = "There was nothing to redo.";
                return false;
            }
        }

        /// <summary>
        /// Close the editing session
        /// </summary>
        /// <param name="error">The reason why we were unable to close.</param>
        /// <returns>True if the model system is saved.</returns>
        public bool Close(ref string error)
        {
            lock (SessionLock)
            {
                if (ModelSystemModel.IsDirty)
                {
                    error = "The project has changed and has not been saved.";
                    return false;
                }
                this.Dispose();
                return true;
            }
        }

        /// <summary>
        /// Forces the session to terminate, even if we will lose data.
        /// </summary>
        private void ForceClose()
        {
            lock (SessionLock)
            {
                if (this.EditingModelSystem)
                {
                    Runtime.ModelSystemController.ReleaseEditingSession(this);
                }
                else
                {
                    this.ProjectEditingSession.ModelSystemEditingSessionClosed(this, this.ModelSystemIndex);
                }
            }
        }

        /// <summary>
        /// Check to see if we are editing the given model system
        /// </summary>
        /// <param name="modelSystem"></param>
        /// <returns></returns>
        internal bool IsEditing(ModelSystem modelSystem)
        {
            return ModelSystem == modelSystem;
        }

        /// <summary>
        /// This gets called after a session has been terminated by either
        /// the modeller controller or the project session.
        /// </summary>
        internal void SessionTerminated()
        {
            var temp = SessionClosed;
            if (temp != null)
            {
                temp(this, new EventArgs());
            }
        }

        private void Dispose(bool destructor)
        {
            this.ForceClose();
        }

        public void Dispose()
        {
            Dispose(false);
        }

        public ModelSystemStructureModel GetRoot(ModelSystemStructureModel currentModule)
        {
            return ModelSystemModel.GetModelFor(currentModule.RealModelSystemStructure.GetRoot(ModelSystemModel.Root.RealModelSystemStructure));
        }

        public ModelSystemStructureModel GetParent(ModelSystemStructureModel currentModule)
        {
            return ModelSystemModel.GetModelFor(currentModule.RealModelSystemStructure.GetParent(ModelSystemModel.Root.RealModelSystemStructure));
        }
    }
}