using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>
/// Writes a voted nickname into a Pokémon sitting in a PC box, with the game running (1.0.7.3): a capture with a full
/// party goes to the box, and waiting for the game to close took the fun out of the vote.
/// </summary>
/// <remarks>
/// <para>
/// The box is not a known address, so the Pokémon is found by what it is: its encryption constant is stored in the clear
/// at the start of the record, followed by two zero bytes. <b>One</b> search of the heap per rename (never a sweep in a
/// loop, which is what brings Azahar down), and every hit is decrypted and believed only if it is that PID with a valid
/// checksum.
/// </para>
/// <para>
/// Only the stored block is written, and only the bytes that change, like the party rename and the level cap: the name
/// and the checksum. Read back before it is called done. False when it is not found; the caller then waits for the game to
/// close and writes the save.
/// </para>
/// </remarks>
public sealed class LiveBoxRenamer(AzaharRpcClient client, ILogger<LiveBoxRenamer> logger)
{
    private const uint Heap = 0x30000000, HeapSize = 0x04000000;
    private static readonly int Stored = new PK7().SIZE_STORED;

    public bool Rename(uint pid, uint encryptionConstant, string nickname)
    {
        try
        {
            if (client.GetProcess() == uint.MaxValue) return false;

            var pattern = new byte[6];
            BitConverter.GetBytes(encryptionConstant).CopyTo(pattern, 0);
            var mask = Enumerable.Repeat((byte)0xFF, pattern.Length).ToArray();

            var renamed = 0;
            foreach (var hit in client.SearchMemory(Heap, HeapSize, pattern, mask))
            {
                if (!client.TryReadMemory(hit, Stored, out var original)) continue;

                // PKHeX descifra en el sitio: siempre sobre una copia.
                var pokemon = new PK7((byte[])original.Clone());
                if (!pokemon.ChecksumValid || pokemon.PID != pid || pokemon.EncryptionConstant != encryptionConstant || pokemon.IsEgg) continue;

                pokemon.SetNickname(nickname);
                pokemon.RefreshChecksum();
                var party = new byte[pokemon.SIZE_PARTY];
                pokemon.WriteEncryptedDataParty(party);

                for (var i = 0; i < Stored; i++)
                {
                    if (party[i] != original[i]) client.WriteMemory((uint)(hit + i), [party[i]]);
                }

                if (client.TryReadMemory(hit, Stored, out var after) && new PK7(after) is { ChecksumValid: true } check
                    && check.PID == pid && check.Nickname == nickname)
                {
                    renamed++;
                    logger.LogInformation("0x{Address:X8}: mote «{Nickname}» ({Pid:X8}) con el juego abierto", hit, nickname, pid);
                }
            }

            return renamed > 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo poner el mote en directo al Pokémon {Pid:X8}", pid);
            return false;
        }
    }
}
