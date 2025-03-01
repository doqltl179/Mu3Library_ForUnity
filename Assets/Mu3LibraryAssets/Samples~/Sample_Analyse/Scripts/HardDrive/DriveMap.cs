using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Mu3Library.Demo.Analyse {
    public class DriveMap {
        public DriveInfo CurrentDrive => currentDrive;
        private DriveInfo currentDrive;

        public DirInfo CurrentDriveInfo => currentDriveInfo;
        private DirInfo currentDriveInfo;

        public bool IsLoading => tokenSource != null;

        private CancellationTokenSource tokenSource = null;



        public DriveMap(DriveInfo drive) {
            currentDrive = drive;
        }

        #region Utility
        public void CancelLoad() {
            if(tokenSource != null) {
                tokenSource.Cancel();
            }
        }

        public async Task LoadDirectories() {
            if(currentDrive == null) {
                Debug.LogWarning($"Current Dirve is NULL.");

                return;
            }

            DateTime startTime = DateTime.Now;

            tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;

            currentDriveInfo = new DirInfo(0, null, currentDrive.RootDirectory);
            await Task.Run(async () => {
                await currentDriveInfo.LoadInfos(tokenSource);
            });

            DateTime endTime = DateTime.Now;
            Debug.Log($"Loaded All Directories. Root: {currentDrive.RootDirectory}, time: {(endTime - startTime).TotalMilliseconds / 1000f:F2}s");

            tokenSource.Dispose();
            tokenSource = null;
        }

        private long GetDirectorySize(DirectoryInfo info) {
            long result = 0;

            try {
                foreach(FileInfo file in info.EnumerateFiles()) {
                    result += file.Length;
                }

                foreach(DirectoryInfo directory in info.EnumerateDirectories()) {
                    result += GetDirectorySize(directory);
                }
            }
            catch {
                Debug.LogWarning($"This Directory can not Readable. path: {info.FullName}");
            }

            return result;
        }
        #endregion
    }

    public class DirInfo {
        public DirInfo Parent => parent;
        private DirInfo parent;

        public DirectoryInfo CurrentDirectory => currentDirectory;
        private DirectoryInfo currentDirectory;

        public DirInfo[] Dirs => dirs;
        private DirInfo[] dirs = new DirInfo[0];

        public FileInfo[] Files => files;
        private FileInfo[] files = new FileInfo[0];

        public bool IsReadable => isReadable;
        private bool isReadable = false;

        public int Depth => depth;
        private int depth;

        public ulong Size => size;
        private ulong size;

        public ulong CompressedSize => compressedSize;
        private ulong compressedSize;



        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetCompressedFileSize(string lpFileName, out uint lpFileSizeHigh);

        public DirInfo(int depth, DirInfo parent, DirectoryInfo directory) {
            this.parent = parent;

            currentDirectory = directory;
            this.depth = depth;
        }

        public async Task LoadInfos(CancellationTokenSource tokenSource) {
            isReadable = true;
            size = 0;
            compressedSize = 0;

            // 파일 읽기 테스트
            try {
                files = currentDirectory.GetFiles();
                if(files.Length > 0) {
                    Parallel.ForEach(files, fi => {
                        if(tokenSource.Token.IsCancellationRequested) {
                            tokenSource.Token.ThrowIfCancellationRequested();
                        }

                        size += (ulong)fi.Length;

                        uint high;
                        uint low = GetCompressedFileSize(fi.FullName, out high);
                        if(low != 0xFFFFFFFF) {
                            compressedSize += (ulong)(((long)high << 32) + low);
                        }
                    });
                }
            }
            catch {
                isReadable = false;
            }

            // 폴더 읽기 테스트
            try {
                DirectoryInfo[] directories = currentDirectory.GetDirectories();
                dirs = new DirInfo[directories.Length];
                if(dirs.Length > 0) {
                    Parallel.For(0, dirs.Length, async (idx) => {
                        if(tokenSource.Token.IsCancellationRequested) {
                            tokenSource.Token.ThrowIfCancellationRequested();
                        }

                        try {
                            DirInfo newInfo = new DirInfo(depth + 1, this, directories[idx]);
                            await newInfo.LoadInfos(tokenSource);

                            size += newInfo.Size;
                            compressedSize += newInfo.CompressedSize;

                            dirs[idx] = newInfo;
                        }
                        catch {

                        }
                    });
                }
            }
            catch {
                isReadable = false;
            }
        }
    }
}