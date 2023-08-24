// This file is part of HugoChecker - A GitHub Action to check Hugo markdown files.
// Copyright (c) Krzysztof Prusik and contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Generic;

namespace HugoChecker;

public class FolderModel
{
    public FolderModel(string fullFolderPath, HugoCheckerConfig config)
    {
        FullFolderPath = fullFolderPath;
        Config = config;
    }

    /// <summary>
    ///     Full folder path to the folder to check.
    /// </summary>
    public string FullFolderPath { get; } 
    
    /// <summary>
    ///     Config for this folder read from hugo-checker.yaml file.
    /// </summary>
    public HugoCheckerConfig Config { get; }

    /// <summary>
    ///     All files in this folder.
    ///         key: full file path to the default file, example c:\wwwroot\content\post\post1.md
    ///         value: file model containing all files with the same name but different language.
    /// </summary>
    public Dictionary<string, FileModel> Files { get; set; } = new();

    /// <summary>
    ///     All files in this folder that are duplicates of other files.
    ///     key: key in header that is duplicated, example title
    ///         value:
    ///             key: value of the key in header that is duplicated, example post1
    ///             value: full path to the file that is duplicated, example c:\wwwroot\content\post\post1.md
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> ProcessedDuplicates { get; set; } = new();
}