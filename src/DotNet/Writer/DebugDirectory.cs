// dnlib: See LICENSE.txt for more info

using System;
using System.Collections.Generic;
using System.IO;
using dnlib.DotNet.Pdb;
using dnlib.IO;
using dnlib.PE;

namespace dnlib.DotNet.Writer {
	/// <summary>
	/// Debug directory entry
	/// </summary>
	public sealed class DebugDirectoryEntry {
		/// <summary>
		/// Gets the header
		/// </summary>
		public IMAGE_DEBUG_DIRECTORY DebugDirectory;

		/// <summary>
		/// Gets the data
		/// </summary>
		public readonly IChunk Chunk;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="chunk">Data</param>
		public DebugDirectoryEntry(IChunk chunk) => Chunk = chunk;
	}

	/// <summary>
	/// Debug directory chunk
	/// </summary>
	public sealed class DebugDirectory : IReuseChunk {
		/// <summary>Default debug directory alignment</summary>
		public const uint DEFAULT_DEBUGDIRECTORY_ALIGNMENT = 4;
		internal const int HEADER_SIZE = 28;

		internal int Count => entries.Count;

		FileOffset offset;
		RVA rva;
		uint length;
		readonly List<DebugDirectoryEntry> entries;
		bool isReadonly;

		/// <inheritdoc/>
		public FileOffset FileOffset => offset;

		/// <inheritdoc/>
		public RVA RVA => rva;

		/// <summary>
		/// Constructor
		/// </summary>
		public DebugDirectory() => entries = new List<DebugDirectoryEntry>();

		/// <summary>
		/// Adds data
		/// </summary>
		/// <param name="data">Data</param>
		/// <returns></returns>
		public DebugDirectoryEntry Add(byte[] data) => Add(new ByteArrayChunk(data));

		/// <summary>
		/// Adds data
		/// </summary>
		/// <param name="chunk">Data</param>
		/// <returns></returns>
		public DebugDirectoryEntry Add(IChunk chunk) {
			if (isReadonly)
				throw new InvalidOperationException("Can't add a new DebugDirectory entry when the DebugDirectory is read-only!");
			var entry = new DebugDirectoryEntry(chunk);
			entries.Add(entry);
			return entry;
		}

		bool IReuseChunk.CanReuse(uint origSize) {
			uint newLength = GetLength(entries, 0, 0);
			if (newLength > origSize)
				return false;

			isReadonly = true;
			return true;
		}

		/// <inheritdoc/>
		public void SetOffset(FileOffset offset, RVA rva) {
			isReadonly = true;
			this.offset = offset;
			this.rva = rva;

			length = GetLength(entries, offset, rva);
		}

		static uint GetLength(List<DebugDirectoryEntry> entries, FileOffset offset, RVA rva) {
			uint length = HEADER_SIZE * (uint)entries.Count;
			foreach (var entry in entries) {
				length = Utils.AlignUp(length, DEFAULT_DEBUGDIRECTORY_ALIGNMENT);
				entry.Chunk.SetOffset(offset + length, rva + length);
				length += entry.Chunk.GetFileLength();
			}
			return length;
		}

		/// <inheritdoc/>
		public uint GetFileLength() => length;

		/// <inheritdoc/>
		public uint GetVirtualSize() => GetFileLength();

		/// <inheritdoc/>
		public void WriteTo(BinaryWriter writer) {
			uint offset = 0;
			foreach (var entry in entries) {
				writer.Write(entry.DebugDirectory.Characteristics);
				writer.Write(entry.DebugDirectory.TimeDateStamp);
				writer.Write(entry.DebugDirectory.MajorVersion);
				writer.Write(entry.DebugDirectory.MinorVersion);
				writer.Write((uint)entry.DebugDirectory.Type);
				writer.Write(entry.Chunk.GetFileLength());
				writer.Write((uint)entry.Chunk.RVA);
				writer.Write((uint)entry.Chunk.FileOffset);
				offset += HEADER_SIZE;
			}

			foreach (var entry in entries) {
				WriteAlign(writer, ref offset);
				entry.Chunk.VerifyWriteTo(writer);
				offset += entry.Chunk.GetFileLength();
			}
		}

		static void WriteAlign(BinaryWriter writer, ref uint offs) {
			uint align = Utils.AlignUp(offs, DEFAULT_DEBUGDIRECTORY_ALIGNMENT) - offs;
			offs += align;
			writer.WriteZeros((int)align);
		}
	}
}
