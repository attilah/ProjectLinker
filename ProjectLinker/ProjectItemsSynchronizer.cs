//===================================================================================
// Microsoft patterns & practices
// Composite Application Guidance for Windows Presentation Foundation and Silverlight
//===================================================================================
// Copyright (c) Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===================================================================================
// The example companies, organizations, products, domain names,
// e-mail addresses, logos, people, places, and events depicted
// herein are fictitious.  No association with any real company,
// organization, product, domain name, email address, logo, person,
// places, or events is intended or should be inferred.
//===================================================================================
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using EnvDTE;
using Microsoft.Practices.ProjectLinker.Utility;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Constants = EnvDTE.Constants;

namespace Microsoft.Practices.ProjectLinker
{
    public class ProjectItemsSynchronizer : IProjectItemsSynchronizer
    {
        private readonly ILogger logger;
        private readonly IVsSolution solution;
        private readonly IHierarchyHelper hierarchyHelper;
        private readonly IProjectItemsFilter projectItemsFilter;

        public ProjectItemsSynchronizer(Project sourceProject, Project targetProject, ILogger logger, IVsSolution solution, IProjectItemsFilter projectItemsFilter)
            : this(sourceProject, targetProject, logger, solution, new HierarchyHelper(), projectItemsFilter)
        {
        }

        public ProjectItemsSynchronizer(Project sourceProject, Project targetProject, ILogger logger, IVsSolution solution, IHierarchyHelper hierarchyHelper, IProjectItemsFilter projectItemsFilter)
        {
            this.logger = logger;
            this.solution = solution;
            this.hierarchyHelper = hierarchyHelper;
            SourceProject = sourceProject;
            TargetProject = targetProject;
            this.projectItemsFilter = projectItemsFilter;
        }

        public Project SourceProject { get; private set; }
        public Project TargetProject { get; private set; }

        public void FileAddedToSource(string file)
        {
            ProjectItem addedFile = GetProjectItemForPath(SourceProject.ProjectItems, file);
            if (addedFile != null)
            {
                string relativePath = GetPathRelativeToProject(addedFile);

                if (this.projectItemsFilter.CanBeSynchronized(relativePath))
                {
                    ProjectItems targetCollection = LocateOrCreateCollection(TargetProject.ProjectItems,
                                                                             Path.GetDirectoryName(relativePath), true);

                    if (targetCollection != null)
                    {
                        try
                        {
                            targetCollection.AddFromFile(file);
                            logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.FileSuccessfullyLinked,
                                                     relativePath, TargetProject.Name));
                        }
                        catch (COMException comEx)
                        {
                            if (comEx.ErrorCode == -2147467259) //HRESULT 0x80004005
                            {
                                logger.Log(string.Format(CultureInfo.CurrentCulture,
                                                         Resources.FileAlreadyExistsInFileSystem, relativePath,
                                                         TargetProject.Name));
                            }
                        }
                    }
                }
                else
                {
                    logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.ItemExcludedByFilter, file, TargetProject.Name));
                }
            }
        }

        public void FileRemovedFromSource(string file)
        {
            ProjectItem targetItem = GetProjectItemForPath(TargetProject.ProjectItems, file);

            if (targetItem != null)
            {
                AssertCanRemoveFile(targetItem);
                string relativePath = GetPathRelativeToProject(targetItem);
                targetItem.Delete();
                logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.FileLinkSuccessfullyRemoved, relativePath, TargetProject.Name));
            }
            else
            {
                logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.FileToRemoveDoesNotExistInTarget, file, TargetProject.Name));
            }
        }

        public void FileRenamedInSource(string oldFileName, string newFileName)
        {
            FileRemovedFromSource(oldFileName);
            FileAddedToSource(newFileName);
        }

        public void DirectoryAddedToSource(string directory)
        {
            string directoryPath = directory;
            if (directoryPath.EndsWith(new string(Path.DirectorySeparatorChar, 1), StringComparison.Ordinal))
            {
                directoryPath = directoryPath.Substring(0, directoryPath.Length - 1);
            }
            string relativePath = GetPathRelativeToProject(SourceProject, directoryPath);

            if (this.projectItemsFilter.CanBeSynchronized(relativePath))
            {
                LocateOrCreateCollection(TargetProject.ProjectItems, relativePath, true);
            }
            else
            {
                logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.ItemExcludedByFilter, directory, TargetProject.Name));
            }
        }

        public void DirectoryRemovedFromSource(string directory)
        {
            string relativePath = GetPathRelativeToProject(SourceProject, directory);
            ProjectItems rootCollection = TargetProject.ProjectItems;
            ProjectItem targetItem = LocateItem(rootCollection, relativePath) as ProjectItem;

            if (targetItem != null)
            {
                if (CanRemoveFolder(targetItem))
                {
                    targetItem.Delete();
                    logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.FolderSuccessfullyRemoved, relativePath, TargetProject.Name));
                }
                else
                {
                    logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.FolderToRemoveIsNotEmpty, relativePath, TargetProject.Name));
                }
            }
        }

        public void DirectoryRenamedInSource(string oldDirectoryName, string newDirectoryName)
        {
            DirectoryRemovedFromSource(oldDirectoryName);
            DirectoryAddedToSource(newDirectoryName);
        }

        private static string GetPathRelativeToProject(ProjectItem projectItem)
        {
            StringBuilder sb = new StringBuilder(projectItem.Name);
            projectItem = projectItem.Collection.Parent as ProjectItem;
            while (projectItem != null)
            {
                sb.Insert(0, "\\");
                sb.Insert(0, projectItem.Name);
                projectItem = projectItem.Collection.Parent as ProjectItem;
            }
            return sb.ToString();
        }

        private static ProjectItem GetProjectItemForPath(ProjectItems collection, string path)
        {
            foreach (ProjectItem item in collection)
            {
                string itemPath = item.Properties.GetValue<string>("FullPath", null);
                if (!string.IsNullOrEmpty(itemPath) && itemPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
                ProjectItem retVal = GetProjectItemForPath(item.ProjectItems, path);
                if (retVal != null)
                {
                    return retVal;
                }
            }
            return null;
        }

        private static string GetPathRelativeToProject(Project project, string path)
        {
            string projectDir = Path.GetDirectoryName(project.FullName);
            Debug.Assert(path.Contains(projectDir), string.Format(CultureInfo.CurrentCulture, "Path '{0}' does not include the project directory ('{1}').", path, projectDir));

            string fileNameWithoutProjectDir = path.Replace(projectDir, "");
            if (fileNameWithoutProjectDir.StartsWith(new string(Path.DirectorySeparatorChar, 1), StringComparison.Ordinal))
            {
                fileNameWithoutProjectDir = fileNameWithoutProjectDir.Substring(1);
            }
            return fileNameWithoutProjectDir;
        }

        private object LocateItem(ProjectItems collection, string relativePath)
        {
            ProjectItems childrenCollection = LocateOrCreateCollection(collection, relativePath, false);
            if (childrenCollection != null)
                return childrenCollection.Parent;

            return null;
        }

        private ProjectItems LocateOrCreateCollection(ProjectItems collection, string relativePath, bool createIfNotExists)
        {
            string[] hierarchyItems = relativePath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            ProjectItems currentCollection = collection;
            foreach (string item in hierarchyItems)
            {
                ProjectItem targetFolder = GetProjectItemByName(currentCollection, item);
                if (targetFolder == null)
                {
                    if (createIfNotExists)
                    {
                        targetFolder = AddOrCreateFolder(item, currentCollection);
                    }
                    else
                    {
                        return null;
                    }
                }
                currentCollection = targetFolder.ProjectItems;
            }
            return currentCollection;
        }

        private ProjectItem AddOrCreateFolder(string folderName, ProjectItems parentCollection)
        {
            try
            {
                ProjectItem addedFolderProjectItem = parentCollection.AddFolder(folderName, Constants.vsProjectItemKindPhysicalFolder);
                string relativePath = GetPathRelativeToProject(TargetProject, addedFolderProjectItem.Properties.GetValue("FullPath", string.Empty));
                logger.Log(String.Format(CultureInfo.CurrentCulture, Resources.FolderCreatedInTarget, relativePath, TargetProject.Name));

                return addedFolderProjectItem;
            }
            catch (COMException comEx)
            {
                if (comEx.ErrorCode == -2147467259) //HRESULT 0x80004005
                {
                    return IncludeExistingFolder(folderName, parentCollection);
                }
                throw;
            }
        }

        private ProjectItem IncludeExistingFolder(string folderName, ProjectItems collection)
        {
            IVsHierarchy targetHierarchy = TargetProject.GetVsHierarchy(solution);
            IVsProject targetIVsProject = targetHierarchy as IVsProject;
            if (targetIVsProject != null)
            {
                string folderAbsolutePath;
                ProjectItem parentProjectItem = collection.Parent as ProjectItem;
                if (parentProjectItem != null)
                {
                    folderAbsolutePath = Path.Combine(parentProjectItem.Properties.GetValue("FullPath", string.Empty), folderName);
                }
                else
                {
                    folderAbsolutePath = Path.Combine(Path.GetDirectoryName(TargetProject.FullName), folderName);
                }
                string containerFolderRelativePath = GetPathRelativeToProject(TargetProject, Path.GetDirectoryName(folderAbsolutePath));

                uint containerFolderId = hierarchyHelper.GetItemId(targetHierarchy, containerFolderRelativePath);
                VSADDRESULT[] result = new VSADDRESULT[1];

                int hr = targetIVsProject.AddItem(containerFolderId,
                                                  VSADDITEMOPERATION.VSADDITEMOP_OPENFILE,
                                                  string.Empty, //No file name because it's a directory
                                                  1, // Has to correspond to #items below...
                                                  new[] { folderAbsolutePath }, // Full path to item
                                                  IntPtr.Zero, // Don't show window
                                                  result); // Result array...
                ErrorHandler.ThrowOnFailure(hr);
                logger.Log(string.Format(CultureInfo.CurrentCulture, Resources.IncludingExistingFolderInProject, GetPathRelativeToProject(TargetProject, folderAbsolutePath), TargetProject.Name));
            }
            return collection.Item(folderName);
        }

        private static ProjectItem GetProjectItemByName(ProjectItems items, string name)
        {
            foreach (ProjectItem item in items)
            {
                if (item.Name == name)
                {
                    return item;
                }
            }
            return null;
        }

        [Conditional("DEBUG")]
        private static void AssertCanRemoveFile(ProjectItem targetItem)
        {
            Debug.Assert(targetItem.Kind == Constants.vsProjectItemKindPhysicalFile
                         && targetItem.Properties.GetValue<bool>("IsLink", false) == true);
        }

        private static bool CanRemoveFolder(ProjectItem targetItem)
        {
            if (targetItem.Kind != Constants.vsProjectItemKindPhysicalFolder)
                return false;

            foreach (ProjectItem item in targetItem.ProjectItems)
            {
                if (!CanRemoveFolder(item))
                    return false;
            }

            return true;
        }
    }
}