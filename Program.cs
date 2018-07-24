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

  static uint mk (string str)
  {
    if (str.Length != 4) throw new Exception ("non 4-character magic???");
    return (uint)(str[3]) << 0 | (uint)(str[2]) << 8
         | (uint)(str[1]) << 16 | (uint)(str[0]) << 24;
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
      try
      {
        Console.WriteLine("-- processing {0}", map);

        System.Drawing.Bitmap[,] tiles = new System.Drawing.Bitmap[64,64];
        bool[,] had_tile = new bool[64,64];
        bool[,] wdt_claims_tile = new bool[64,64];

        var wdt_name = Path.Combine("world", "maps", map, String.Format("{0}.wdt", map));
        try
        {
          using (Stream stream = cascHandler.OpenFile(wdt_name))
          {
            using (BinaryReader reader = new BinaryReader(stream))
            {
              while (reader.BaseStream.Position != reader.BaseStream.Length)
              {
                var magic = reader.ReadUInt32();
                var size = reader.ReadUInt32();
                var pos = reader.BaseStream.Position;

                if (magic == mk ("MPHD"))
                {
                  var flags = reader.ReadUInt32();

                  if ((flags & 1) == 1)
                  {
                    throw new Exception ("map claims to be WMO only, skipping!");
                  }
                }
                else if (magic == mk ("MAIN"))
                {
                  for (int x = 0; x < 64; ++x)
                  {
                    for (int y = 0; y < 64; ++y)
                    {
                      wdt_claims_tile[y,x] = (reader.ReadUInt32() & 1) == 1;
                      reader.ReadUInt32();
                    }
                  }
                }

                reader.BaseStream.Position = pos + size;
              }
            }
          }
        }
        catch (FileNotFoundException)
        {
          throw new Exception (String.Format("failed loading {0}, skipping!", wdt_name));
        }

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
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.Yellow)
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.Red)
               );
            var wdt_border_brush = new System.Drawing.Drawing2D.HatchBrush
               ( System.Drawing.Drawing2D.HatchStyle.DiagonalCross
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.DarkBlue)
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.Red)
               );
            var unknown_brush = new System.Drawing.Drawing2D.HatchBrush
               ( System.Drawing.Drawing2D.HatchStyle.LargeGrid
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.Black)
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.Transparent)
               );
            var wdt_border_pen = new System.Drawing.Pen
               (wdt_border_brush, size_per_mcnk);
            var unreferenced_brush = new System.Drawing.Drawing2D.HatchBrush
               ( System.Drawing.Drawing2D.HatchStyle.DiagonalCross
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.DarkBlue)
               , System.Drawing.Color.FromArgb (255/2, System.Drawing.Color.Green)
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
                    var magic = reader.ReadUInt32();
                    var size = reader.ReadUInt32();
                    var pos = reader.BaseStream.Position;

                    if (magic == mk ("MCNK"))
                    {
                      var flags = reader.ReadUInt32();
                      var sub_x = reader.ReadUInt32();
                      var sub_y = reader.ReadUInt32();
					  var nLayers = reader.ReadUInt32();
					  var nDoodadRefs = reader.ReadUInt32();
					  var holes_high_res = reader.ReadUInt64();
					  var ofsLayer = reader.ReadUInt32();
					  var ofsRefs = reader.ReadUInt32();
					  var ofsAlpha = reader.ReadUInt32();
					  var sizeAlpha = reader.ReadUInt32();
					  var ofsShadow = reader.ReadUInt32();
					  var sizeShadow = reader.ReadUInt32();
					  var areaid = reader.ReadUInt32();
                      if ((flags & 2) == 2)
                        g.FillRectangle(impassable_brush, size_per_mcnk * sub_x, size_per_mcnk * sub_y, size_per_mcnk, size_per_mcnk);
					
                      if (areaid == 0)
                        g.FillRectangle(unknown_brush, size_per_mcnk * sub_x, size_per_mcnk * sub_y, size_per_mcnk, size_per_mcnk);
                    }

                    reader.BaseStream.Position = pos + size;
                  }
                }
              }
              had_tile[x,y] = true;
            }
            catch (FileNotFoundException)
            {
              g.FillRectangle(wdt_border_brush, 0, 0, tiles[x,y].Height, tiles[x,y].Height);
            }

            if (wdt_claims_tile[x,y])
            {
              if (x == 0 || !wdt_claims_tile[x-1,y])
              {
                g.DrawLine(wdt_border_pen, 0, 0, 0, tiles[x,y].Height);
              }
              if (x == 63 || !wdt_claims_tile[x+1,y])
              {
                g.DrawLine(wdt_border_pen, tiles[x,y].Height, 0, tiles[x,y].Height, tiles[x,y].Height);
              }
              if (y == 0 || !wdt_claims_tile[x,y-1])
              {
                g.DrawLine(wdt_border_pen, 0, 0, tiles[x,y].Height, 0);
              }
              if (y == 63 || !wdt_claims_tile[x,y+1])
              {
                g.DrawLine(wdt_border_pen, 0, tiles[x,y].Height, tiles[x,y].Height, tiles[x,y].Height);
              }
            }
            else if (had_tile[x,y])
            {
              g.FillRectangle(unreferenced_brush, 0, 0, tiles[x,y].Height, tiles[x,y].Height);
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
      catch (Exception ex)
      {
        Console.WriteLine("--- {0}", ex.Message);
      }
    }

    return 0;
  }
}