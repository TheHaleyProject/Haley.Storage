﻿using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using Microsoft.Identity.Client;

namespace Haley.Services {

    //We can store the version either as separate files or as individual versions. Its totally upto us.
    public partial class DiskStorageService : IDiskStorageService {
        public async Task<IOSSResponse> Upload(IOSSWrite input) {
            OSSResponse result = new OSSResponse() {
                Status = false,
                RawName = input.FileOriginalName
            };
            try {
                if (!WriteMode) {
                    result.Message = "Application is in Read-Only mode.";
                    return result;
                }
                if (input == null) {
                    result.Message = "Input cannot be empty or null";
                    return result;
                }

                var gPaths = ProcessAndBuildStoragePath(input, true);
                if (string.IsNullOrWhiteSpace(input.TargetPath)) {
                    result.Message = "Unable to generate the final storage path. Please check inputs.";
                    return result;
                }

                //What if there is some extension and is missing??
                if (string.IsNullOrWhiteSpace(Path.GetExtension(gPaths.basePath))) {
                    string exten = string.Empty;
                    //Extension is missing. Lets figure out if we have somewhere. 
                    //Check if target name has it or the origianl filename has it.
                    do {
                        exten = Path.GetExtension(input.TargetName);
                        if (!string.IsNullOrWhiteSpace(exten)) break;
                        exten = Path.GetExtension(input.FileOriginalName);
                        if (!string.IsNullOrWhiteSpace(exten)) break;
                        if (input.FileStream != null && input.FileStream is FileStream fs) {
                            exten = Path.GetExtension(fs.Name);
                        }
                    } while (false); //One time event
                    if (!string.IsNullOrWhiteSpace(exten) && !gPaths.targetPath.EndsWith(exten)) {
                        gPaths.targetPath += $@"{exten}";
                    }
                }

                if (input.BufferSize < 4096) input.BufferSize = 4096; //Default CopyTo from System.IO has 80KB buffersize. We setit as 4KB for fast storage.

                if (input.FileStream == null) throw new ArgumentException($@"File stream is null. Nothing to save.");
                input.FileStream.Position = 0; //Precaution

                if (input.TargetPath == gPaths.basePath) throw new ArgumentException($@"No file save name is processed.");

                if (!ShouldProceedFileUpload(result, input.TargetPath, input.ResolveMode)) return result;

                //Either file doesn't exists.. or exists and replace

                if (!result.ObjectExists || input.ResolveMode == OSSResolveMode.Replace) {
                    //TODO : DEFERRED REPLACEMENT
                    //If the file is currently in use, try for 5 times and then replace. May be easy option would be to store in temporary place and then update a database that a temporary file is created and then later, with some background process check the database and try to replace. This way we dont' have to block the api call or wait for completion.
                    await input.FileStream?.TryReplaceFileAsync(input.TargetPath, input.BufferSize);
                } else if (input.ResolveMode == OSSResolveMode.Revise) {
                    //Then we revise the file and store in same location.
                    //First get the current version name.. and then 
                    if (DirectoryUtils.PopulateVersionedPath(Path.GetDirectoryName(input.TargetPath), input.TargetPath, out var version_path)) {
                        //File exists.. and we also have the name using which we should replace it.
                        //Try copy the file under current name
                        try {
                            //First copy the current file to new version path and then 
                            if (await DirectoryUtils.TryCopyFileAsync(input.TargetPath, version_path)) {
                                //Copy success
                                await input.FileStream?.TryReplaceFileAsync(input.TargetPath, input.BufferSize);
                            }
                        } catch (Exception) {
                            await DirectoryUtils.TryDeleteFile(version_path);
                        }
                    }
                }

                if (!result.ObjectExists) {
                    result.Message = "Uploaded."; //For skip also, we will return true (but object will exists)
                }
                result.Status = true;
                if (input.File != null) result.SetResult(input.File);
            } catch (Exception ex) {
                result.Message = ex.Message;
                result.Status = false;
            } finally {
                if (input != null && Indexer != null && input.Module != null && input.File != null) {
                    //We try to make a call to the db to update the information about the file version info.
                    var upInfo = await Indexer.UpdateDocumentInfo(input.Module.Cuid, input.File);
                    Console.WriteLine($@"Document version update status: {upInfo.Status} {Environment.NewLine} Result : {upInfo.Result.ToString()}");
                }
            }
            return result;
        }
        public Task<IOSSFileStreamResponse> Download(IOSSRead input, bool auto_search_extension = true) {
            IOSSFileStreamResponse result = new FileStreamResponse() { Status = false, Stream = Stream.Null };
            var path = ProcessAndBuildStoragePath(input,  true, readonlyMode: true).targetPath;
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(result);

            if (!File.Exists(path) && auto_search_extension) {
                //If file extension is not present, then search the targetpath for matching filename and fetch the object (if only one is present).

                if (string.IsNullOrWhiteSpace(Path.GetExtension(path))) {
                    var findName = Path.GetFileNameWithoutExtension(path); //If the extension is not available, obviously it will return only file name. But, what if the file is like 'test.' ends with a period (.). So, we use the GetFileNameWithoutExtension method.

                    //Extension not provided. So, lets to see if we have any matching file.
                    DirectoryInfo dinfo = new DirectoryInfo(Path.GetDirectoryName(path));
                    if (!dinfo.Exists) {
                        result.Message = "The directory doesn't exists.";
                        return Task.FromResult(result);
                    }
                    var matchingFiles = dinfo?.GetFiles()?.Where(p => Path.GetFileNameWithoutExtension(p.Name) == findName).ToList();
                    if (matchingFiles.Count() == 1) {
                        path = matchingFiles.FirstOrDefault().FullName;
                    } else if (matchingFiles.Count() > 1) {
                        //We found mathing items but more than one
                        result.Message = "Multiple matching files found. Please provide a valid extension.";
                        return Task.FromResult(result);
                    }
                }
            }

            if (!File.Exists(path)) {
                result.Message = "File doesn't exist.";
                return Task.FromResult(result);
            }
            result.Status = true;
            result.Extension = Path.GetExtension(path);
            result.Stream = new FileStream(path, FileMode.Open, FileAccess.Read) as Stream;
            return Task.FromResult(result); //Stream is open here.
        }
        public async Task<IFeedback> Delete(IOSSRead input) {
            IFeedback feedback = new Feedback() { Status = false };
            if (!WriteMode) {
                feedback.Message = "Application is in Read-Only mode.";
                return feedback;
            }
            var path = ProcessAndBuildStoragePath(input, true, readonlyMode: true).targetPath;

            if (string.IsNullOrWhiteSpace(path)) {
                feedback.Message = "Unable to generate path from provided inputs.";
                return feedback;
            }

            if (!File.Exists(path)) {
                feedback.Message = $@"File does not exists : {path}.";
                return feedback;
            }

            feedback.Status = await path.TryDeleteFile();
            feedback.Message = feedback.Status ? "File deleted" : "Unable to delete the file. Check if it is in use by other process & try again.";
            return feedback;
        }
        public IFeedback Exists(IOSSRead input, bool isFilePath = false) {
            var feedback = new Feedback() { Status = false };
            var path = ProcessAndBuildStoragePath(input, isFilePath, readonlyMode: true).targetPath;
            if (string.IsNullOrWhiteSpace(path)) {
                feedback.Message = "Unable to generate path from provided inputs.";
                return feedback;
            }
            if (isFilePath) {
                feedback.Status = File.Exists(path);
            } else {
                feedback.Status = Directory.Exists(path);
            }
            if (!feedback.Status) feedback.Message = $@"Does not exists {path}";
            return feedback;
        }


        public long GetSize(IOSSRead input) {
            var path = ProcessAndBuildStoragePath(input, true, readonlyMode: true).targetPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return 0;
            return new FileInfo(path).Length;
        }

        public Task<IOSSDirResponse> GetDirectoryInfo(IOSSRead input) {
            IOSSDirResponse result = new OSSDirResponse() { Status = false };
            var path = ProcessAndBuildStoragePath(input, false, readonlyMode: true).targetPath;
            if (string.IsNullOrWhiteSpace(path)) {
                result.Message = "Unable to generate path.";
                return Task.FromResult(result);
            }

            //It is users' responsibility to send a valid path for checking.
            if (!Directory.Exists(path)) {
                result.Message = "Unable to find the specified path. Please check.";
                return Task.FromResult(result);
            }

            var dinfo = new DirectoryInfo(path);

            result.FoldersList = dinfo.GetDirectories()?.Select(p => p.Name)?.ToList();
            result.FilesList = dinfo.GetFiles()?.Select(p => p.Name)?.ToList();
            return Task.FromResult(result);
        }

        public async Task<IOSSResponse> CreateDirectory(IOSSRead input, string rawname) {
            IOSSResponse result = new OSSResponse() {
                Status = false,
                RawName = rawname
            };
            try {
                if (!WriteMode) {
                    result.Message = "Application is in Read-Only mode.";
                    return result;
                }
                var path = ProcessAndBuildStoragePath(input, false, readonlyMode: true).targetPath;

                if (string.IsNullOrWhiteSpace(path)) {
                    result.Message = $@"Unable to generate the path. Please check inputs.";
                    return result;
                }

                if (Directory.Exists(path)) {
                    result.Status = true;
                    result.Message = $@"Directory already exists.";
                    return result;
                }
                if (!(await path?.TryCreateDirectory())) {
                    result.Message = $@"Unable to create the directory. Please check if it is valid.";
                    return result;
                }

                result.Status = true;
                result.Message = "Created";
            } catch (Exception ex) {
                result.Status = false;
                result.Message = ex.Message;
            }
            return result;
        }

        public async Task<IFeedback> DeleteDirectory(IOSSRead input, bool recursive) {
            IFeedback feedback = new Feedback() { Status = false };
            if (!WriteMode) {
                feedback.Message = "Application is in Read-Only mode.";
                return feedback;
            }
            var pathInfo = ProcessAndBuildStoragePath(input, false, readonlyMode: true);
            var path = pathInfo.targetPath;
            if (string.IsNullOrWhiteSpace(path)) {
                feedback.Message = "Unable to generate path from provided inputs.";
                return feedback;
            }
            //How do we verfiy, if this the final target that we wish to delete?
            //We should not by mistake end up deleting a wrong directory.
            var expectedToDelete = input.Folder.Path?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(expectedToDelete) ||
                (expectedToDelete == "\\" || expectedToDelete == "/") ||
                expectedToDelete.Equals(pathInfo.basePath.ToLower()) ||
                 pathInfo.targetPath.Equals(pathInfo.basePath,StringComparison.InvariantCultureIgnoreCase)) {
                feedback.Message = "Path is not valid for deleting.";
                return feedback;
            }
            if (!Directory.Exists(path)) {
                feedback.Message = $@"Directory does not exists. : {path}.";
                return feedback;
            }
            //Directory.Delete(path, recursive);
            await path?.TryDeleteDirectory();
            feedback.Status = true;
            feedback.Message = "Deleted successfully";
            return feedback;
        }

        bool ShouldProceedFileUpload(IOSSResponse result, string filePath, OSSResolveMode conflict) {

            var targetDir = Path.GetDirectoryName(filePath); //Get only the directory.
            //Should we even try to generate the directory first???
            if (!(targetDir?.TryCreateDirectory().Result ?? false)) {
                result.Message = $@"Unable to ensure storage directory. Please check if it is valid. {targetDir}";
                return false;
            }

            if (!filePath.StartsWith(BasePath)) {
                result.Message = "Not authorized for this folder. Please check the path.";
                return false;
            }

            result.ObjectExists = File.Exists(filePath);
            if (result.ObjectExists) {
                switch (conflict) {
                    case OSSResolveMode.Skip:
                    result.Status = true;
                    result.Message = "File exists. Skipped";
                    return false; //DONT PROCESS FURTHER
                    case OSSResolveMode.ReturnError:
                    result.Status = false;
                    result.Message = $@"File Exists. Returned Error.";
                    return false; //DONT PROCESS FURTHER
                    case OSSResolveMode.Replace:
                    result.Message = "Replace initiated";
                    return true; //PROCESS FURTHER
                    case OSSResolveMode.Revise:
                    result.Message = "File revision initiated";
                    return true; //PROCESS FURTHER
                }
            }
            return true;
        }
    }
}
