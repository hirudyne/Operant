using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Operant.SaveFormat;

/// <summary>
/// A loaded Zero Parades save file. <see cref="Header"/> and <see cref="Content"/>
/// are mutable JSON DOMs; call <see cref="Write"/> or <see cref="Serialize"/> to
/// produce a fresh, re-checksummed, re-encrypted file.
/// </summary>
/// <remarks>
/// <para>File layout (BinaryWriter framing; all integers little-endian):</para>
/// <list type="bullet">
///   <item><c>string  ContentTag</c>          — 7-bit length-prefixed UTF-8, "C4SaveFileHeader"</item>
///   <item><c>int32   Version</c>             — 1</item>
///   <item><c>int32   length + bytes</c>      — plaintext UTF-8 JSON header</item>
///   <item><c>string  Hash128Hex</c>          — 32-char hex checksum of the header chunk</item>
///   <item><c>byte    EncryptionRequired</c>  — 0x01 in observed saves</item>
///   <item><c>string  ContentTag</c>          — "C4SaveFileContent"</item>
///   <item><c>int32   Version</c>             — 0</item>
///   <item><c>int32   length + bytes</c>      — encrypted content: salt(32)|iv(16)|cipher</item>
///   <item><c>string  Hash128Hex</c>          — 32-char hex checksum of the content chunk</item>
/// </list>
/// <para>Per-chunk checksum is <see cref="Hash128.Compute"/>(ContentTag, Version, DataBlob),
/// where DataBlob is plaintext for the header and the encrypted bytes for the content.</para>
/// </remarks>
public class SaveFile
{
    public const string HeaderTag = "C4SaveFileHeader";
    public const string ContentTag = "C4SaveFileContent";
    public const int HeaderVersion = 1;
    public const int ContentVersion = 0;

    public JsonObject Header { get; set; }
    public JsonObject Content { get; set; }
    public bool EncryptionRequired { get; set; }

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private SaveFile(JsonObject header, JsonObject content, bool encryptionRequired)
    {
        Header = header;
        Content = content;
        EncryptionRequired = encryptionRequired;
    }

    // --- I/O --------------------------------------------------------------

    public static SaveFile Read(string path)
    {
        return Parse(File.ReadAllBytes(path));
    }

    public void Write(string path)
    {
        File.WriteAllBytes(path, Serialize());
    }

    public static SaveFile Parse(byte[] data)
    {
        using var ms = new MemoryStream(data, writable: false);
        using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: false);

        string tag = br.ReadString();
        if (tag != HeaderTag)
            throw new SaveFormatException($"expected '{HeaderTag}' but got '{tag}'");
        int headerVersion = br.ReadInt32();
        byte[] headerBlob = ReadByteArray(br);
        string headerHashFile = br.ReadString();
        string headerHashComputed = Hash128.Compute(HeaderTag, headerVersion, headerBlob);
        if (headerHashFile != headerHashComputed)
            throw new SaveChecksumException("header", headerHashFile, headerHashComputed);

        byte encryptionByte = br.ReadByte();
        bool encryptionRequired = encryptionByte != 0;

        tag = br.ReadString();
        if (tag != ContentTag)
            throw new SaveFormatException($"expected '{ContentTag}' but got '{tag}'");
        int contentVersion = br.ReadInt32();
        byte[] contentBlob = ReadByteArray(br);
        string contentHashFile = br.ReadString();
        string contentHashComputed = Hash128.Compute(ContentTag, contentVersion, contentBlob);
        if (contentHashFile != contentHashComputed)
            throw new SaveChecksumException("content", contentHashFile, contentHashComputed);

        byte[] plainContent = encryptionRequired
            ? SaveEncryption.Decrypt(contentBlob)
            : contentBlob;

        JsonObject header = ParseJsonObject(headerBlob, "header");
        JsonObject content = ParseJsonObject(plainContent, "content");

        if (headerVersion != HeaderVersion || contentVersion != ContentVersion)
        {
            throw new SaveFormatException(
                $"unrecognised chunk versions: header={headerVersion} content={contentVersion}; " +
                "the file may have been written by a different game build.");
        }

        return new SaveFile(header, content, encryptionRequired);
    }

    public byte[] Serialize()
    {
        FlushFeldState();
        byte[] headerBlob = Encoding.UTF8.GetBytes(Header.ToJsonString(JsonWriteOptions));
        byte[] contentPlain = Encoding.UTF8.GetBytes(Content.ToJsonString(JsonWriteOptions));
        byte[] contentBlob = EncryptionRequired
            ? SaveEncryption.Encrypt(contentPlain)
            : contentPlain;

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(HeaderTag);
            bw.Write(HeaderVersion);
            WriteByteArray(bw, headerBlob);
            bw.Write(Hash128.Compute(HeaderTag, HeaderVersion, headerBlob));
            bw.Write(EncryptionRequired ? (byte)1 : (byte)0);
            bw.Write(ContentTag);
            bw.Write(ContentVersion);
            WriteByteArray(bw, contentBlob);
            bw.Write(Hash128.Compute(ContentTag, ContentVersion, contentBlob));
        }
        return ms.ToArray();
    }

    // --- High-level accessors --------------------------------------------

    /// <summary>
    /// Sol (currency). Mirrored between the header SummaryData and the
    /// <c>stats.money</c> counter in FELDState; setter updates both.
    /// </summary>
    public int Sol
    {
        get => (int?)Header["SummaryData"]?["Sol"] ?? 0;
        set
        {
            EnsureObject(Header, "SummaryData")["Sol"] = value;
            SetCounter("stats.money", value, createIfMissing: false);
        }
    }

    /// <summary>
    /// The deserialised <c>FELDState</c> JSON. Edits propagate when
    /// <see cref="Serialize"/> is next called.
    /// </summary>
    public JsonObject FeldState
    {
        get
        {
            _feldCache ??= ParseFeldState();
            return _feldCache;
        }
    }

    private JsonObject? _feldCache;

    private JsonObject ParseFeldState()
    {
        var node = Content["FELDState"];
        if (node is null)
            throw new SaveFormatException("content has no FELDState field");
        string text = node.GetValue<string>();
        var parsed = JsonNode.Parse(text) as JsonObject
                     ?? throw new SaveFormatException("FELDState is not a JSON object");
        return parsed;
    }

    public void FlushFeldState()
    {
        if (_feldCache is null) return;
        Content["FELDState"] = _feldCache.ToJsonString(JsonWriteOptions);
    }

    /// <summary>
    /// Inventory items as a mutable list view backed by the JSON DOM.
    /// </summary>
    public JsonArray Inventory
    {
        get
        {
            var inv = EnsureObject(Content, "Inventory");
            if (inv["Items"] is not JsonArray arr)
            {
                arr = new JsonArray();
                inv["Items"] = arr;
            }
            return arr;
        }
    }

    public bool TryGetCounter(string key, out int value)
    {
        var keys = (JsonArray?)FeldState["m_countersValues"]?["m_keys"];
        var vals = (JsonArray?)FeldState["m_countersValues"]?["m_values"];
        if (keys is null || vals is null) { value = 0; return false; }
        for (int i = 0; i < keys.Count; i++)
        {
            if ((string?)keys[i] == key)
            {
                value = (int?)vals[i] ?? 0;
                return true;
            }
        }
        value = 0;
        return false;
    }

    public void SetCounter(string key, int value, bool createIfMissing = true)
    {
        var counters = EnsureObject(FeldState, "m_countersValues");
        if (counters["m_keys"] is not JsonArray keys)
        {
            keys = new JsonArray();
            counters["m_keys"] = keys;
        }
        if (counters["m_values"] is not JsonArray vals)
        {
            vals = new JsonArray();
            counters["m_values"] = vals;
        }
        for (int i = 0; i < keys.Count; i++)
        {
            if ((string?)keys[i] == key)
            {
                vals[i] = value;
                return;
            }
        }
        if (createIfMissing)
        {
            keys.Add(key);
            vals.Add(value);
        }
    }

    public IEnumerable<(string Key, int Value)> EnumerateCounters()
    {
        var keys = (JsonArray?)FeldState["m_countersValues"]?["m_keys"];
        var vals = (JsonArray?)FeldState["m_countersValues"]?["m_values"];
        if (keys is null || vals is null) yield break;
        int n = Math.Min(keys.Count, vals.Count);
        for (int i = 0; i < n; i++)
            yield return ((string?)keys[i] ?? string.Empty, (int?)vals[i] ?? 0);
    }

    // --- Helpers ----------------------------------------------------------

    private static byte[] ReadByteArray(BinaryReader br)
    {
        int length = br.ReadInt32();
        if (length < 0) throw new SaveFormatException($"negative array length {length}");
        return br.ReadBytes(length);
    }

    private static void WriteByteArray(BinaryWriter bw, byte[] data)
    {
        bw.Write(data.Length);
        bw.Write(data);
    }

    private static JsonObject ParseJsonObject(byte[] data, string label)
    {
        try
        {
            return JsonNode.Parse(data) as JsonObject
                   ?? throw new SaveFormatException($"{label} JSON is not an object");
        }
        catch (JsonException e)
        {
            throw new SaveFormatException($"failed to parse {label} JSON: {e.Message}", e);
        }
    }

    private static JsonObject EnsureObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing) return existing;
        var fresh = new JsonObject();
        parent[key] = fresh;
        return fresh;
    }
}
