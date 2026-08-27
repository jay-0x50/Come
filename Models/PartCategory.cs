namespace Come.Models;

public enum PartCategory { Cpu, Mainboard, Memory, Graphics, Storage, Power, Case, Cooler }

public sealed record CategoryOption(PartCategory Value, string Name, string EnglishName, string Glyph);
