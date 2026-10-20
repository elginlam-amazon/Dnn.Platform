// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.FileSystem.Internal;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using DotNetNuke.ComponentModel;

/// <summary>The default <see cref="IDirectory"/> implementation.</summary>
public class DirectoryWrapper : ComponentBase<IDirectory, DirectoryWrapper>, IDirectory
{
    /// <inheritdoc />
    public void Delete(string path, bool recursive)
    {
        if (this.Exists(path))
        {
            Directory.Delete(path, recursive);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string path, bool recursive)
    {
        if (await this.ExistsAsync(path))
        {
            await Task.Run(() => Directory.Delete(path, recursive));
        }
    }

    /// <inheritdoc />
    public bool Exists(string path)
    {
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string path)
    {
        return await Task.Run(() => Directory.Exists(path));
    }

    /// <inheritdoc />
    public string[] GetDirectories(string path)
    {
        return Directory.GetDirectories(path);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetDirectoriesAsync(string path)
    {
        return await Task.Run(() => Directory.EnumerateDirectories(path));
    }

    /// <inheritdoc />
    public string[] GetFiles(string path)
    {
        return Directory.GetFiles(path);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetFilesAsync(string path)
    {
        return await Task.Run(() => Directory.EnumerateFiles(path));
    }

    /// <inheritdoc />
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetFiles(path, searchPattern, searchOption);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetFilesAsync(string path, string searchPattern, SearchOption searchOption)
    {
        return await Task.Run(() => Directory.EnumerateFiles(path, searchPattern, searchOption));
    }

    /// <inheritdoc />
    public void Move(string sourceDirName, string destDirName)
    {
        Directory.Move(sourceDirName, destDirName);
    }

    /// <inheritdoc />
    public async Task MoveAsync(string sourceDirName, string destDirName)
    {
        await Task.Run(() => Directory.Move(sourceDirName, destDirName));
    }

    /// <inheritdoc />
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    public async Task CreateDirectoryAsync(string path)
    {
        await Task.Run(() => Directory.CreateDirectory(path));
    }

    /// <inheritdoc />
    public void CreateDirectory(string path, bool recursive)
    {
        // Directory.CreateDirectory always creates all missing parent directories
        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    public async Task CreateDirectoryAsync(string path, bool recursive)
    {
        await Task.Run(() => Directory.CreateDirectory(path));
    }
}
