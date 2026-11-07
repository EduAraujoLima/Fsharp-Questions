using System.Security.Cryptography;

namespace Questions.Api.Services;

/// <summary>Gera identificadores (efeito colateral isolado do núcleo funcional).</summary>
public interface IIdGenerator
{
    Guid NewId();
}

public sealed class GuidV7Generator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}

/// <summary>Fornece a entropia (semente) para o gerador pseudoaleatório puro do F#.</summary>
public interface ISeedProvider
{
    long NextSeed();
}

public sealed class CryptoSeedProvider : ISeedProvider
{
    // Limitada a 53 bits para ser representável sem perda em clientes JavaScript.
    private const long MaxSafeInteger = (1L << 53) - 1;

    public long NextSeed() => BitConverter.ToInt64(RandomNumberGenerator.GetBytes(sizeof(long))) & MaxSafeInteger;
}
