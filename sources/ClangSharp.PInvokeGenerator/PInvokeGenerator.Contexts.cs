// Copyright (c) .NET Foundation and Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using SelectorFunc = System.Func<System.Collections.Generic.LinkedListNode<(ClangSharp.Cursor Cursor, object? UserData)>,
    System.Collections.Generic.LinkedListNode<(ClangSharp.Cursor Cursor, object? UserData)>?>;

namespace ClangSharp;

public partial class PInvokeGenerator
{
    /// <summary>
    /// RAII wrapper for <see cref="_context"/>. Release by <see cref="ContextScope.Dispose"/>
    /// </summary>
    private ref struct ContextScope
    {
        private LinkedListNode<(Cursor, object?)> _node;
        private readonly PInvokeGenerator _parent;

        public ContextScope(Cursor cursor, object? context, PInvokeGenerator parent)
        {
            _parent = parent;

            var ctxLinkedList = _parent._context;

            _node = ctxLinkedList.AddLast((cursor, context));
        }

        public void Dispose()
        {
            if (_node is not null)
            {
                PopContext();

                _node = null!;
            }

        }


        [SuppressMessage("Style", "IDE0251:将成员设为“readonly”", Justification = "Mutate operation to _context")]
        private void PopContext()
        {
            var ctxLinkedList = _parent._context;

            var lastContextNode = ctxLinkedList.Last;
            var scopeMatch = lastContextNode == _node;
            Debug.Assert(scopeMatch, "Improperly paired Push & Pop calls to _context");

            if (scopeMatch)
            {
                ctxLinkedList.RemoveLast();
            }
        }
    }

    [SuppressMessage("Style", "IDE0250:将结构设置为 “readonly”", Justification = "This struct allows caller to modify context")]
    private ref struct Context<TCursor, TUserData> where TCursor : Cursor where TUserData : notnull
    {
        [AllowNull]
        private readonly ref object? _userRef;

        private Context(TCursor c, ref object? user, LinkedListNode<(Cursor, object?)> selectedNode)
        {
            _userRef = ref user;
            Cursor = c;
            TargetContextNode = selectedNode;
        }

        public readonly LinkedListNode<(Cursor Cursor, object? UserData)>? TargetContextNode { get; }

        public readonly TCursor? Cursor { get; }

        [MemberNotNullWhen(true, nameof(TargetContextNode), nameof(Cursor))]
        public readonly bool IsValid => TargetContextNode is not null;

        [MemberNotNullWhen(true, nameof(TargetContextNode), nameof(Cursor), nameof(_userRef), nameof(UserData))]
        public readonly bool HasValue => IsValid && _userRef is not null;

        public TUserData? UserData
        {
            readonly get
            {
                Debug.Assert(IsValid);
                return (TUserData?)_userRef;
            }
            [SuppressMessage("Style", "IDE0251:将成员设为“readonly”", Justification = "Mutates context itself")]
            set
            {
                Debug.Assert(IsValid);
                _userRef = value;
            }
        }

        /// <devdoc>
        /// <summary>
        /// Attempt to match a wrapper <see cref="Context{, }"/> starting from the previous context of <see cref="TargetContextNode"/>
        /// </summary>
        /// <typeparam name="TNewCursor"></typeparam>
        /// <typeparam name="TNewUserData"></typeparam>
        /// <param name="ctx">Valid if returned <see langword="true"/>.</param>
        /// <param name="selectContext">Pass <see langword="null"/> to match the previous of <see cref="TargetContextNode"/> only.</param>
        /// <returns>The <paramref name="ctx"> is valid when return <see langword="true"/>.</returns>
        /// </devdoc>
        internal readonly bool TryMatchPreviousContexts<TNewCursor, TNewUserData>(
            [MaybeNullWhen(false)] out Context<TNewCursor, TNewUserData> ctx,
            SelectorFunc? selectContext = null)
            where TNewCursor : Cursor where TNewUserData : notnull
        {
            Debug.Assert(IsValid);
            var previousContext = TargetContextNode.Previous;
            Debug.Assert(previousContext is not null);

            return Context<TNewCursor, TNewUserData>.MatchOnceFrom(previousContext, out ctx, selectContext);
        }

        internal readonly Context<TNewCursor, TUserData> CastCursor<TNewCursor>()
            where TNewCursor : TCursor
        {
            Debug.Assert(IsValid);
            return new((TNewCursor)Cursor, ref _userRef, TargetContextNode);
        }

        internal static bool IsContextTypeMatch(Cursor c, object? user, [NotNullWhen(true)] out TCursor? goodCursor)
        {
            if (c is TCursor cursor)
            {
                goodCursor = cursor;

                return typeof(TUserData) == typeof(object)
                    || user == null // do not remove this line
                    || user is TUserData;
                // due to `TUserData` can be a class
                // plus `user is TUserData?` is equivalent to `user is TUserData` until C# 14 with no diagnostics
                // because the spec does not require `TUserData?` considered as a nullable reference type 
                // such rules result in `null is TUserData?` surprisingly evaluate to `false` when `TUserData` is a class
            }
            else
            {
                goodCursor = null;
                return false;
            }
        }

        internal static bool MatchOnceFrom(LinkedListNode<(Cursor Cursor, object? UserData)> contextNode,
            [MaybeNullWhen(false)] out Context<TCursor, TUserData> ctx,
            SelectorFunc? selectContext = null)
        {
            Debug.Assert(contextNode is not null);
            contextNode = selectContext?.Invoke(contextNode) ?? contextNode;

            var mappedCursor = contextNode.ValueRef.Cursor;
            ref var user = ref contextNode.ValueRef.UserData;

            if (IsContextTypeMatch(mappedCursor, user, out var goodCursor))
            {
                ctx = new(goodCursor, ref user, contextNode);
                return true;
            }

            ctx = default;
            return false;
        }

        internal static bool MatchAllContextsFrom(LinkedListNode<(Cursor Cursor, object? UserData)> contextNode,
            [MaybeNullWhen(false)] out Context<TCursor, TUserData> ctx,
            SelectorFunc? selectContext = null)
        {
            LinkedListNode<(Cursor Cursor, object? UserData)>? circularCheckPrevNode = null;
            var selectedNode = selectContext?.Invoke(contextNode) ?? contextNode;

            while (selectedNode is not null)
            {
                if ((circularCheckPrevNode?.Value.Cursor ?? null) == selectedNode.ValueRef.Cursor)
                {
                    // Selector returned the node which does not
                    // match the requirements since the last loop.
                    // Restart the matching from the prev context node again. Or infinite loop will occur.
                    circularCheckPrevNode = null;
                    selectedNode = selectedNode.Previous;
                    continue;
                }

                if (MatchOnceFrom(selectedNode, out ctx, null))
                {
                    return true;
                }

                circularCheckPrevNode = selectedNode;
                selectedNode = selectContext?.Invoke(selectedNode);
            }

            ctx = default;
            return false;
        }
    }
}
