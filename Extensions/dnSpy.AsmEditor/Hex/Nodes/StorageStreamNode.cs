﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet.MD;
using dnSpy.AsmEditor.Properties;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Hex;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.TreeView;

namespace dnSpy.AsmEditor.Hex.Nodes {
	enum StorageStreamType {
		None,
		Strings,
		US,
		Blob,
		Guid,
		Tables,
		Pdb,
		HotHeap,
	}

	sealed class StorageStreamNode : HexNode {
		public override Guid Guid => new Guid(DocumentTreeViewConstants.STRGSTREAM_NODE_GUID);
		public override NodePathName NodePathName => new NodePathName(Guid, StreamNumber.ToString());
		public StorageStreamType StorageStreamType { get; }
		public override object VMObject => storageStreamVM;
		protected override ImageReference IconReference => DsImages.BinaryFile;
		public int StreamNumber { get; }

		protected override IEnumerable<HexVM> HexVMs {
			get { yield return storageStreamVM; }
		}

		readonly StorageStreamVM storageStreamVM;

		public StorageStreamNode(HexBuffer buffer, StreamHeader sh, int streamNumber, DotNetStream knownStream, IMetaData md)
			: base(HexSpan.FromBounds((ulong)sh.StartOffset, (ulong)sh.EndOffset)) {
			StreamNumber = streamNumber;
			StorageStreamType = GetStorageStreamType(knownStream);
			storageStreamVM = new StorageStreamVM(this, buffer, Span.Start, (int)(Span.Length - 8).ToUInt64());

			var tblStream = knownStream as TablesStream;
			if (tblStream != null)
				newChild = new TablesStreamNode(buffer, tblStream, md);
		}
		TreeNodeData newChild;

		public override IEnumerable<TreeNodeData> CreateChildren() {
			if (newChild != null)
				yield return newChild;
			newChild = null;
		}

		static StorageStreamType GetStorageStreamType(DotNetStream stream) {
			if (stream == null)
				return StorageStreamType.None;
			if (stream is StringsStream)
				return StorageStreamType.Strings;
			if (stream is USStream)
				return StorageStreamType.US;
			if (stream is BlobStream)
				return StorageStreamType.Blob;
			if (stream is GuidStream)
				return StorageStreamType.Guid;
			if (stream is TablesStream)
				return StorageStreamType.Tables;
			if (stream.Name == "#Pdb")
				return StorageStreamType.Pdb;
			if (stream.Name == "#!")
				return StorageStreamType.HotHeap;
			Debug.Fail(string.Format("Shouldn't be here when stream is a known stream type: {0}", stream.GetType()));
			return StorageStreamType.None;
		}

		public override void OnBufferChanged(NormalizedHexChangeCollection changes) {
			base.OnBufferChanged(changes);
			if (changes.OverlapsWith(storageStreamVM.RCNameVM.Span))
				TreeNode.RefreshUI();

			foreach (HexNode node in TreeNode.DataChildren)
				node.OnBufferChanged(changes);
		}

		protected override void WriteCore(ITextColorWriter output, DocumentNodeWriteOptions options) {
			output.Write(BoxedTextColor.HexStorageStream, dnSpy_AsmEditor_Resources.HexNode_StorageStream);
			output.WriteSpace();
			output.Write(BoxedTextColor.Operator, "#");
			output.Write(BoxedTextColor.Number, StreamNumber.ToString());
			output.Write(BoxedTextColor.Punctuation, ":");
			output.WriteSpace();
			output.Write(StorageStreamType == StorageStreamType.None ? BoxedTextColor.HexStorageStreamNameInvalid : BoxedTextColor.HexStorageStreamName, string.Format("{0}", storageStreamVM.RCNameVM.StringZ));
		}

		public MetaDataTableRecordNode FindTokenNode(uint token) {
			if (StorageStreamType != StorageStreamType.Tables)
				return null;
			return ((TablesStreamNode)TreeNode.Children[0].Data).FindTokenNode(token);
		}
	}
}
