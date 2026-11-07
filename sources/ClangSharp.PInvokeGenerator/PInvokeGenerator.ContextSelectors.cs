// Copyright (c) .NET Foundation and Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using SelectorFunc = System.Func<System.Collections.Generic.LinkedListNode<(ClangSharp.Cursor Cursor, object? UserData)>,
    System.Collections.Generic.LinkedListNode<(ClangSharp.Cursor Cursor, object? UserData)>?>;

namespace ClangSharp;

public partial class PInvokeGenerator
{
    private static class ContextSelector
    {
        internal static SelectorFunc LastCursor<TCursor>()
            where TCursor : Cursor
        {
            return node => {
                while (node is { ValueRef.Cursor: not TCursor })
                {
                    var prev = node.Previous;
                    Debug.Assert(prev is not null, "No context is satisfied the requirement");
                    node = prev;
                }

                return node;
            };
        }

        internal static SelectorFunc LastDataPresent<TCursor, TData>()
            where TCursor : Cursor where TData : notnull
        {
            return node => {
                while (node is { ValueRef: { Cursor: not TCursor, UserData: null or not TData } })
                {
                    var prev = node.Previous;
                    Debug.Assert(prev is not null, "No context is satisfied the requirement");
                    node = prev;
                }

                return node;
            };
        }
    }
}
