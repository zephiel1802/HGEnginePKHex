using System;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core;

/// <summary>
/// <see cref="SaveFile"/> format for HeartGold Engine (<see cref="SaveFileType.HGE"/>)
/// supporting 30 PC Boxes and expanded species/moves/abilities.
/// </summary>
public sealed class SAV4HGE : SAV4HGSS
{
    public const int BoxCountHGE = 30;
    public const int StorageSizeHGE = 0x1E510;
    private const int GeneralGapHGE = 0xD8;

    private const int BOX_SLOTS = 30;
    private const int BOX_NAME_LEN = 40; // 20 characters
    private const int BOX_DATA_LEN = (BOX_SLOTS * PokeCrypto.SIZE_4STORED) + 0x10; // 0x1000 per box
    private const int BOX_END_HGE = BoxCountHGE * BOX_DATA_LEN; // 30 * 0x1000 = 0x1E000
    private const int BOX_NAME_HGE = 0x1E008; // after current (4) & counter (4)
    private const int BOX_WP_HGE = BOX_NAME_HGE + (BoxCountHGE * BOX_NAME_LEN); // 0x1E008 + 0x4B0 = 0x1E4B8
    private const int BOX_FLAGS_HGE = BoxCountHGE + BOX_WP_HGE; // 0x1E4B8 + 30 = 0x1E4D6

    public SAV4HGE() : base(GeneralSize, StorageSizeHGE)
    {
    }

    public SAV4HGE(Memory<byte> data) : base(data, GeneralSize, StorageSizeHGE, GeneralSize + GeneralGapHGE)
    {
    }

    public override int BoxCount => BoxCountHGE;
    public override ushort MaxSpeciesID => Legal.MaxSpeciesID_6; // 721 (Volcanion)
    public override ushort MaxMoveID => Legal.MaxMoveID_6_AO; // 621 (Hyperspace Fury)
    public override int MaxAbilityID => 512; // hg-engine supports up to 512 abilities

    public override IPersonalTable Personal => PersonalTable.AO;

    public override int GetBoxOffset(int box) => box * 0x1000;
    private static int GetBoxNameOffset(int box) => BOX_NAME_HGE + (box * BOX_NAME_LEN);
    private static int GetBoxWallpaperOffset(int box) => BOX_WP_HGE + box;

    public override int CurrentBox
    {
        get => Storage[BOX_END_HGE];
        set => Storage[BOX_END_HGE] = (byte)value;
    }

    public override byte[] BoxFlags
    {
        get => [ Storage[BOX_FLAGS_HGE] ];
        set => Storage[BOX_FLAGS_HGE] = value[0];
    }

    public override int FlagsBoxContentChanged
    {
        get => ReadInt32LittleEndian(Storage[(BOX_END_HGE + 4)..]);
        set => WriteInt32LittleEndian(Storage[(BOX_END_HGE + 4)..], value);
    }

    protected override int FlagsBoxContentChangedAll => (1 << BoxCountHGE) - 1; // 0x3FFF_FFFF (30 boxes)

    protected override Span<byte> GetBoxNameSpan(int box) => Storage.Slice(GetBoxNameOffset(box), BOX_NAME_LEN);
    public override string GetBoxName(int box) => GetString(GetBoxNameSpan(box));

    public override void SetBoxName(int box, ReadOnlySpan<char> value)
    {
        const int maxlen = 8;
        var span = GetBoxNameSpan(box);
        SetString(span, value, maxlen, StringConverterOption.ClearZero);
    }

    public override int GetBoxWallpaper(int box)
    {
        int offset = GetBoxWallpaperOffset(box);
        int value = Storage[offset];
        if (value >= 0x10)
            return value - 0x10;
        return value;
    }

    public override void SetBoxWallpaper(int box, int value)
    {
        if (value >= 0x10)
            value += 0x10;
        Storage[GetBoxWallpaperOffset(box)] = (byte)value;
    }

    protected override SAV4 CloneInternal4() => State.Exportable ? new SAV4HGE(Data.ToArray()) : new SAV4HGE();
}
