using System;
using System.Buffers.Binary;
using FluentAssertions;
using Xunit;

namespace PKHeX.Core.Tests.Saves;

public class SAV4HGETests
{
    [Fact]
    public void BlankSAV4HGE_InitializesProperly()
    {
        var sav = new SAV4HGE();
        sav.BoxCount.Should().Be(30);
        sav.MaxSpeciesID.Should().Be(721);
        sav.MaxMoveID.Should().Be(621);
        sav.MaxAbilityID.Should().Be(512);
        sav.FlagsBoxContentChanged.Should().Be(0);
    }

    [Fact]
    public void BoxOperations_All30Boxes_ReadAndWrite()
    {
        var sav = new SAV4HGE();

        // Test names and wallpapers for all 30 boxes
        for (int i = 0; i < 30; i++)
        {
            var name = $"BOX {i + 1}";
            sav.SetBoxName(i, name);
            sav.GetBoxName(i).Should().Be(name);

            sav.SetBoxWallpaper(i, i % 16);
            sav.GetBoxWallpaper(i).Should().Be(i % 16);
        }

        // Test placing a Pokémon in Box 30 (index 29), slot 30 (index 29)
        var pk = new PK4
        {
            Species = (ushort)Species.Pikachu,
            CurrentLevel = 50,
            TID16 = 12345,
            SID16 = 54321,
            Nickname = "SPARKY",
        };
        pk.RefreshChecksum();

        sav.SetBoxSlotAtIndex(pk, 29, 29);

        var retrieved = (PK4)sav.GetBoxSlotAtIndex(29, 29);
        retrieved.Species.Should().Be((ushort)Species.Pikachu);
        retrieved.CurrentLevel.Should().Be(50);
        retrieved.Nickname.Should().Be("SPARKY");
    }

    [Fact]
    public void ExpandedSpecies_Gen5And6_HandledProperly()
    {
        var sav = new SAV4HGE();

        // Gen 5 Victini (#494)
        var victini = new PK4
        {
            Species = (ushort)Species.Victini,
            CurrentLevel = 15,
            TID16 = 10001,
            Nickname = "VICTINI",
        };
        victini.RefreshChecksum();
        victini.PersonalInfo.Type1.Should().Be((byte)MoveType.Psychic);
        victini.PersonalInfo.Type2.Should().Be((byte)MoveType.Fire);

        // Gen 6 Greninja (#658)
        var greninja = new PK4
        {
            Species = (ushort)Species.Greninja,
            CurrentLevel = 36,
            Nickname = "GRENINJA",
        };
        greninja.RefreshChecksum();
        greninja.PersonalInfo.Type1.Should().Be((byte)MoveType.Water);
        greninja.PersonalInfo.Type2.Should().Be((byte)MoveType.Dark);

        // Place in Box 25
        sav.SetBoxSlotAtIndex(victini, 24, 0);
        sav.SetBoxSlotAtIndex(greninja, 24, 1);

        var retrievedVictini = (PK4)sav.GetBoxSlotAtIndex(24, 0);
        retrievedVictini.Species.Should().Be((ushort)Species.Victini);

        var retrievedGreninja = (PK4)sav.GetBoxSlotAtIndex(24, 1);
        retrievedGreninja.Species.Should().Be((ushort)Species.Greninja);

        // Legality analysis should not crash
        var laVictini = new LegalityAnalysis(retrievedVictini);
        laVictini.Parsed.Should().BeTrue();
        laVictini.Valid.Should().BeTrue();
    }

    [Fact]
    public void SaveUtil_Detection_DistinguishesHGSSandHGE()
    {
        // 1. Vanilla HGSS raw buffer
        var rawHGSS = new byte[SaveUtil.SIZE_G4RAW];
        BinaryPrimitives.WriteUInt32LittleEndian(rawHGSS.AsSpan(0x40000 + SAV4HGSS.GeneralSize - 0xC), SAV4HGSS.GeneralSize);
        BinaryPrimitives.WriteUInt32LittleEndian(rawHGSS.AsSpan(0x40000 + SAV4HGSS.GeneralSize - 0x8), SAV4.MAGIC_JAPAN_INTL);

        var loadedHGSS = SaveUtil.GetSaveFile(rawHGSS);
        loadedHGSS.Should().NotBeNull();
        loadedHGSS.Should().BeOfType<SAV4HGSS>();
        loadedHGSS!.BoxCount.Should().Be(18);

        // 2. HGE raw buffer with 30-box storage footer
        var rawHGE = new byte[SaveUtil.SIZE_G4RAW];
        BinaryPrimitives.WriteUInt32LittleEndian(rawHGE.AsSpan(0x40000 + SAV4HGSS.GeneralSize - 0xC), SAV4HGSS.GeneralSize);
        BinaryPrimitives.WriteUInt32LittleEndian(rawHGE.AsSpan(0x40000 + SAV4HGSS.GeneralSize - 0x8), SAV4.MAGIC_JAPAN_INTL);

        const int sStart = 0xF700;
        const int sSize30 = SAV4HGE.StorageSizeHGE;
        const int offsetP2Storage = 0x40000 + sStart + sSize30;
        BinaryPrimitives.WriteUInt32LittleEndian(rawHGE.AsSpan(offsetP2Storage - 0xC), sSize30);
        BinaryPrimitives.WriteUInt32LittleEndian(rawHGE.AsSpan(offsetP2Storage - 0x8), SAV4.MAGIC_JAPAN_INTL);

        var loadedHGE = SaveUtil.GetSaveFile(rawHGE);
        loadedHGE.Should().NotBeNull();
        loadedHGE.Should().BeOfType<SAV4HGE>();
        loadedHGE!.BoxCount.Should().Be(30);
    }
}
