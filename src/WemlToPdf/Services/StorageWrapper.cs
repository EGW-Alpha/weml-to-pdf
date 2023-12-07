using Egw.PubManagement.Application.Services;
using Egw.PubManagement.Storage;

namespace WemlToPdf.Services;

/// <inheritdoc />
public class StorageWrapper : IStorageWrapper
{
    /// <inheritdoc />
    public IStorage Covers { get; }

    /// <inheritdoc />
    public IStorage Exports { get; }

    /// <inheritdoc />
    public IStorage Mp3 { get; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public StorageWrapper(IStorage covers, IStorage exports, IStorage mp3)
    {
        Covers = covers;
        Exports = exports;
        Mp3 = mp3;
    }
}