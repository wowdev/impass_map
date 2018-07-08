using CASCLib;
using CommandLine;
using CommandLine.Text;
using System.IO;
using System;

class Program
{
  class Options
  {
    [Option(Required = true, HelpText = "Maps to extract.")]
    public System.Collections.Generic.IEnumerable<string> Maps { get; set; }
    [Option(Required = true, HelpText = "Path for output files.")]
    public string OutputPath { get; set; }

    [Option(HelpText = "Path for offline storage.")]
    public string StoragePath { get; set; }

    [Option(Default = "eu", HelpText = "Region to use for online storage.")]
    public string OnlineRegion { get; set; }
    [Option(Default = "wow", HelpText = "Product to use for online storage.")]
    public string OnlineProduct { get; set; }
    [Option(Default = false, HelpText = "Use online storage.")]
    public bool UseOnline { get; set; }
  }

  static void Main(string[] args)
  {
    try
    {
      CommandLine.Parser.Default.ParseArguments<Options>(args)
        .WithParsed(options => RunAndReturnExitCode(options));
    }
    catch (Exception exc)
    {
      Console.WriteLine(exc.GetType().Name + exc.StackTrace);
      Console.WriteLine(exc.Message);
    }
  }

  static int RunAndReturnExitCode(Options opts)
  {
    if (!Directory.Exists(opts.OutputPath))
        Directory.CreateDirectory(opts.OutputPath);

    CASCHandler cascHandler; 
    if (opts.UseOnline)
    {
      cascHandler = CASCHandler.OpenOnlineStorage(opts.OnlineProduct, opts.OnlineRegion);
    }
    else if (opts.StoragePath != null)
    {
      cascHandler = CASCHandler.OpenLocalStorage(opts.StoragePath);
    }
    else
    {
      throw new Exception ("StoragePath required if not using online mode!");
    }
    cascHandler.Root.SetFlags(LocaleFlags.All_WoW, ContentFlags.None);

    foreach (var map in opts.Maps)
    {
      Console.WriteLine("-- processing {0}", map);

      var wdt_name = Path.Combine("world", "maps", map, String.Format("{0}.wdt", map));
      if (!cascHandler.FileExists(wdt_name))
      {
        Console.WriteLine ("--- {0} does not exist, skipping!", wdt_name);
        continue;
      }

      System.Drawing.Bitmap[,] tiles = new System.Drawing.Bitmap[64,64];
      bool[,] had_tile = new bool[64,64];

      var tile_size = 256;

      for (int x = 0; x < 64; ++x)
      {
        for (int y = 0; y < 64; ++y)
        {
          had_tile[x,y] = false;

          try
          {
            var blp_name = Path.Combine("world", "minimaps", map, String.Format("map{0:00}_{1:00}.blp", x, y));
            using (Stream stream = cascHandler.OpenFile(blp_name))
            {
              var blp = new SereniaBLPLib.BlpFile(stream);
              tiles[x,y] = blp.GetBitmap(0);

              if (tiles[x,y].Height != tiles[x,y].Width) throw new Exception ("non-square minimap?!");
            }
            had_tile[x,y] = true;
          }
          catch (FileNotFoundException)
          {
            tiles[x,y] = new System.Drawing.Bitmap (tile_size, tile_size);
          }

          var size_per_mcnk = tiles[x,y].Height / 16f;

          var g = System.Drawing.Graphics.FromImage(tiles[x,y]);

          var impassable_brush = new System.Drawing.Drawing2D.HatchBrush
             ( System.Drawing.Drawing2D.HatchStyle.DiagonalCross
             , System.Drawing.Color.FromArgb (255/2, 255, 255, 0)
             , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.Red)
             );

          try
          {
            var adt_name = Path.Combine("World", "Maps", map, String.Format("{0}_{1}_{2}.adt", map, x, y));
            using (Stream stream = cascHandler.OpenFile(adt_name))
            {
              using (BinaryReader reader = new BinaryReader(stream))
              {
                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                  var magic = reader.ReadInt32();
                  var size = reader.ReadInt32();
                  var pos = reader.BaseStream.Position;

                  if (magic == 0x4d434e4b /* MCNK */)
                  {
                    var flags = reader.ReadInt32();
                    var sub_x = reader.ReadInt32();
                    var sub_y = reader.ReadInt32();
                    if ((flags & 2) == 2)
                      g.FillRectangle(impassable_brush, size_per_mcnk * sub_x, size_per_mcnk * sub_y, size_per_mcnk, size_per_mcnk);
                  }

                  reader.BaseStream.Position = pos + size;
                }
              }
            }
            had_tile[x,y] = true;
          }
          catch (FileNotFoundException)
          {
          }
        }
      }

      int min_x = 64;
      int min_y = 64;
      int max_x = -1;
      int max_y = -1;

      for (int x = 0; x < 64; ++x)
      {
        for (int y = 0; y < 64; ++y)
        {
          if (had_tile[x,y]) {
            min_x = Math.Min(min_x,x);
            min_y = Math.Min(min_y,y);
            max_x = Math.Max(max_x,x + 1);
            max_y = Math.Max(max_y,y + 1);
          }
        }
      }

      var overall = new System.Drawing.Bitmap (tile_size * (max_x - min_x), tile_size * (max_y - min_y));
      var overall_graphics = System.Drawing.Graphics.FromImage(overall);

      for (int x = min_x; x <= max_x; ++x)
      {
        for (int y = min_y; y <= max_y; ++y)
        {
          if (had_tile[x,y]) {
            overall_graphics.DrawImage(tiles[x,y], (x - min_x) * tile_size, (y - min_y) * tile_size, tile_size, tile_size);
          }
        }
      }

      var output_file = Path.Combine (opts.OutputPath, String.Format("{0}.png", map));
      System.IO.File.Delete (output_file);
      overall.Save(output_file, System.Drawing.Imaging.ImageFormat.Png);
    }

    return 0;
  }
}