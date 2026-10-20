// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.FileSystem.Internal;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// A contract specifying the ability to take actions on directories.
/// A wrapper around functionality from <see cref="Directory"/>.
/// Also provides async overloads which run the <see cref="Directory"/> operations on the thread pool.
/// </summary>
public interface IDirectory
{
    /// <inheritdoc cref="Directory.Delete(string, bool)" />
    void Delete(string path, bool recursive);

    /// <inheritdoc cref="Directory.Delete(string, bool)" />
    Task DeleteAsync(string path, bool recursive);

    /// <inheritdoc cref="Directory.Exists(string)" />
    bool Exists(string path);

    /// <inheritdoc cref="Directory.Exists(string)" />
    Task<bool> ExistsAsync(string path);

    /// <inheritdoc cref="Directory.GetDirectories(string)" />
    string[] GetDirectories(string path);

    /// <inheritdoc cref="Directory.EnumerateDirectories(string)" />
    Task<IEnumerable<string>> GetDirectoriesAsync(string path);

    /// <inheritdoc cref="Directory.GetFiles(string)" />
    string[] GetFiles(string path);

    /// <inheritdoc cref="Directory.GetFiles(string, string, SearchOption)" />
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

    /// <inheritdoc cref="Directory.EnumerateFiles(string, string, SearchOption)" />
    Task<IEnumerable<string>> GetFilesAsync(string path);

    /// <inheritdoc cref="Directory.EnumerateFiles(string, string, SearchOption)" />
    Task<IEnumerable<string>> GetFilesAsync(string path, string searchPattern, SearchOption searchOption);

    /// <inheritdoc cref="Directory.Move(string, string)" />
    void Move(string sourceDirName, string destDirName);

    /// <inheritdoc cref="Directory.Move(string, string)" />
    Task MoveAsync(string sourceDirName, string destDirName);

    /// <inheritdoc cref="Directory.CreateDirectory(string)" />
    void CreateDirectory(string path);

    /// <inheritdoc cref="Directory.CreateDirectory(string)" />
    Task CreateDirectoryAsync(string path);

    /// <inheritdoc cref="Directory.CreateDirectory(string)" />
    void CreateDirectory(string path, bool recursive);

    /// <inheritdoc cref="Directory.CreateDirectory(string)" />
    Task CreateDirectoryAsync(string path, bool recursive);
}
