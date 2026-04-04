using DynamicDungeon.Core;
using DynamicDungeon.Core.Models;

// Legend: # = Wall  . = Floor  S = Spawn  E = Exit  M = Enemy  * = Shortest Path

var parameters = args.Length > 0 ? ParseArgs(args) : PromptUser();
var generator  = new MapGenerator();

Console.WriteLine($"Generating {parameters.Width}x{parameters.Height} {parameters.Biome} map " +
                  $"[{parameters.Algorithm}, {parameters.Difficulty}, seed={parameters.Seed}]...");

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var map = generator.Generate(parameters);
stopwatch.Stop();

Render(map, parameters.ComputeShortestPath);

Console.WriteLine();
Console.WriteLine($"Generated in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Spawn: ({map.SpawnPoint.x},{map.SpawnPoint.y})  Exit: ({map.ExitPoint.x},{map.ExitPoint.y})");
if (parameters.ComputeShortestPath)
    Console.WriteLine($"Shortest path length: {map.ShortestPath.Count} tiles");

static void Render(TileMap map, bool showPath)
{
    var pathSet = showPath
        ? new HashSet<(int, int)>(map.ShortestPath)
        : new HashSet<(int, int)>();

    Console.WriteLine();
    for (int y = 0; y < map.Height; y++)
    {
        for (int x = 0; x < map.Width; x++)
        {
            var tile = map.Get(x, y);
            if (showPath && pathSet.Contains((x, y)) && tile == Tile.Floor)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write('*');
                Console.ResetColor();
                continue;
            }

            (char ch, ConsoleColor color) = tile switch
            {
                Tile.Wall   => ('#', ConsoleColor.DarkGray),
                Tile.Floor  => ('.', ConsoleColor.Gray),
                Tile.Spawn  => ('S', ConsoleColor.Green),
                Tile.Exit   => ('E', ConsoleColor.Red),
                Tile.Enemy  => ('M', ConsoleColor.Yellow),
                _           => ('?', ConsoleColor.White)
            };

            Console.ForegroundColor = color;
            Console.Write(ch);
        }
        Console.WriteLine();
    }
    Console.ResetColor();
}

static GenerationParameters PromptUser()
{
    var p = new GenerationParameters();

    Console.WriteLine("=== Dynamic Dungeon Generator ===");
    Console.WriteLine();

    p.Algorithm = Ask("Algorithm", new[] { "Cellular Automata", "Perlin Noise", "Wave Function Collapse" }) switch
    {
        1 => AlgorithmType.PerlinNoise,
        2 => AlgorithmType.WaveFunctionCollapse,
        _ => AlgorithmType.CellularAutomata
    };

    p.Biome = Ask("Biome", new[] { "Dungeon", "Cave", "Ruins" }) switch
    {
        1 => BiomeType.Cave,
        2 => BiomeType.Ruins,
        _ => BiomeType.Dungeon
    };

    p.Difficulty = Ask("Difficulty", new[] { "Easy", "Medium", "Hard" }) switch
    {
        0 => DifficultyLevel.Easy,
        2 => DifficultyLevel.Hard,
        _ => DifficultyLevel.Medium
    };

    p.Width  = AskInt("Map width",  50, 10, 200);
    p.Height = AskInt("Map height", 50, 10, 200);
    p.Seed   = AskInt("Seed (0 = random)", 0, 0, int.MaxValue);

    if (p.Seed == 0)
        p.Seed = new Random().Next(1, int.MaxValue);

    Console.Write("Show shortest path? (y/n) [n]: ");
    p.ComputeShortestPath = Console.ReadLine()?.Trim().ToLowerInvariant() == "y";

    Console.WriteLine();
    return p;
}

// Displays a numbered menu and returns the 0-based index of the chosen option.
static int Ask(string label, string[] options)
{
    Console.WriteLine($"{label}:");
    for (int i = 0; i < options.Length; i++)
        Console.WriteLine($"  {i + 1}. {options[i]}");

    while (true)
    {
        Console.Write($"  Choice [1]: ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input)) return 0;
        if (int.TryParse(input, out int choice) && choice >= 1 && choice <= options.Length)
            return choice - 1;
        Console.WriteLine($"  Please enter a number between 1 and {options.Length}.");
    }
}

static int AskInt(string label, int defaultValue, int min, int max)
{
    while (true)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input)) return defaultValue;
        if (int.TryParse(input, out int value) && value >= min && value <= max)
            return value;
        Console.WriteLine($"  Please enter a number between {min} and {max}.");
    }
}

static GenerationParameters ParseArgs(string[] args)
{
    var p = new GenerationParameters();

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--algorithm":
                p.Algorithm = args[++i].ToLowerInvariant() switch
                {
                    "perlin" or "pn"  => AlgorithmType.PerlinNoise,
                    "wfc"             => AlgorithmType.WaveFunctionCollapse,
                    _                 => AlgorithmType.CellularAutomata
                };
                break;
            case "--biome":
                p.Biome = args[++i].ToLowerInvariant() switch
                {
                    "cave"  => BiomeType.Cave,
                    "ruins" => BiomeType.Ruins,
                    _       => BiomeType.Dungeon
                };
                break;
            case "--difficulty":
                p.Difficulty = args[++i].ToLowerInvariant() switch
                {
                    "easy" => DifficultyLevel.Easy,
                    "hard" => DifficultyLevel.Hard,
                    _      => DifficultyLevel.Medium
                };
                break;
            case "--width":
                p.Width = int.Parse(args[++i]);
                break;
            case "--height":
                p.Height = int.Parse(args[++i]);
                break;
            case "--seed":
                p.Seed = int.Parse(args[++i]);
                break;
            case "--path":
                p.ComputeShortestPath = true;
                break;
        }
    }

    return p;
}
