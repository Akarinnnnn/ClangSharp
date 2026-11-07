// Copyright (c) .NET Foundation and Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;

namespace ClangSharp.Contexts;

internal sealed class CxxRecordGenerationContextData(CXXRecordDecl rootRecordDecl)
{
    public HashSet<string> GeneratedMethodNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public CXXRecordDecl RootRecordDecl { get; } = rootRecordDecl;
    public CXXRecordDecl? CurrentMethodDeclContext { get; set; }
    public CXXMethodDecl? CurrentMethodDecl { get; set; }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="cxxMethod"></param>
    /// <returns><see langword="false"/> if </returns>
    public bool MarkMethodGenerated(CXXMethodDecl cxxMethod, PInvokeGenerator generator)
        => GeneratedMethodNames.Add(PInvokeGenerator.GetCursorMangledName(cxxMethod));
}
