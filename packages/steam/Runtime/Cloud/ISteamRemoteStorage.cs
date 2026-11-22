using Steamworks;

namespace Mu3Library.Steam.Cloud
{
    /// <summary>
    /// The Steam Remote Storage calls <see cref="SteamCloudSaveStorage"/> makes, so the edit-mode
    /// tests check its file names and its answers to Steam without a Steam client.
    /// </summary>
    internal interface ISteamRemoteStorage
    {
        public bool FileExists(string fileName);
        public int GetFileSize(string fileName);
        public int FileRead(string fileName, byte[] buffer, int size);
        public bool FileWrite(string fileName, byte[] data, int size);
        public bool FileDelete(string fileName);
        public int GetFileCount();
        public string GetFileNameAndSize(int index, out int size);
        public bool IsCloudEnabledForAccount();
        public bool IsCloudEnabledForApp();
    }

    internal sealed class SteamworksRemoteStorage : ISteamRemoteStorage
    {
        public bool FileExists(string fileName) => SteamRemoteStorage.FileExists(fileName);
        public int GetFileSize(string fileName) => SteamRemoteStorage.GetFileSize(fileName);
        public int FileRead(string fileName, byte[] buffer, int size) => SteamRemoteStorage.FileRead(fileName, buffer, size);
        public bool FileWrite(string fileName, byte[] data, int size) => SteamRemoteStorage.FileWrite(fileName, data, size);
        public bool FileDelete(string fileName) => SteamRemoteStorage.FileDelete(fileName);
        public int GetFileCount() => SteamRemoteStorage.GetFileCount();
        public string GetFileNameAndSize(int index, out int size) => SteamRemoteStorage.GetFileNameAndSize(index, out size);
        public bool IsCloudEnabledForAccount() => SteamRemoteStorage.IsCloudEnabledForAccount();
        public bool IsCloudEnabledForApp() => SteamRemoteStorage.IsCloudEnabledForApp();
    }
}
