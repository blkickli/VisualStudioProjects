// Brad L. Kicklighter
// iTunesLib

#define FILL_LIB
#define WRITE_SCHEMA

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using iTunesLib;

namespace iTunesLib
{
  class Program
  {

    public static LogFile logFile;

    // Main
    static void Main(string[] args)
    {
      Console.WriteLine(Environment.MachineName);
      logFile = new LogFile();
      logFile.OutputMajorSeparator();
      logFile.Output("BEGIN");
      // Get music library from or iTunes from existing XML file
      MusicLibrary musicLib = new MusicLibrary();
#if FILL_LIB
      musicLib.FillLib();
      musicLib.WriteLibXML();
#else
      musicLib.ReadLibXML();
#endif
#if WRITE_SCHEMA
      musicLib.WriteLibSchema();
#endif
      musicLib.DisplayStats();
      logFile.Output("END");
      logFile.OutputMajorSeparator();
      logFile.Close();
      Console.ReadKey();
    }  // function Main

  }  // class Program

  // Gets library data from iTunes, organizes it by genre, album artist, album, calculates statistics, provides methods to write library to HTML
  class MusicLibrary
  {
    private static LibraryDataSet lib;  // dataset that contains library data from WMP
    private static Double totalDuration;  // total duration of music in seconds
    private const String PublicMusicLocation = @"C:\Users\Public\Music\";

    // Constructor
    public MusicLibrary()
    {
      lib = new LibraryDataSet();
      totalDuration = 0;
    }  // function MusicLibrary

    // Fill the lib data set from Windows Media Player
    public void FillLib()
    {
      lib.ComputerName.AddComputerNameRow(Environment.MachineName);
      lib.LibrarySource.AddLibrarySourceRow("iTunes");
      Program.logFile.Output("Connecting to iTunes");
      var app = new iTunesApp();  // Object to access iTunes
      var allTracks = app.LibraryPlaylist.Tracks.OfType<IITFileOrCDTrack>().ToList();  // Get all file or CD tracks
      int numMedia = allTracks.Count;
      Program.logFile.Output("Populating music library data set");
      // Process all songs
      int i = 0;
      foreach (var media in allTracks)
      {
        if (media.Genre == "podcast" || media.Genre == "Podcast")
        {
          // Skipping podcast genre
        }
        else if (media.Location == null) // Check for null in location
        {
          Program.logFile.Output("Skipping, location null: " + media.Genre + "|" + media.AlbumArtist + "|" + media.Album + "|" + media.TrackNumber.ToString());
        }
        else if (!media.Location.StartsWith(PublicMusicLocation))  // Check location of track
        {
          // Skipping track that is not located in the public music folder
        }
        else if (media.Genre == "podcast" || media.Genre == "Podcast")
        {
          // Skipping podcast genre
        }
        else if (media.Genre == null || media.AlbumArtist == null || media.Album == null)  // Check for nulls in key fields
        {
          Program.logFile.Output("Skipping nulls: " + media.Genre + "|" + media.AlbumArtist + "|" + media.Album + "|" + media.TrackNumber.ToString());
        }
        else  // Process track
        {
          // Genre
          String genre = media.Genre;
          // Determine if genre has already been captured in lib data set
          LibraryDataSet.GenreRow genreRow = (LibraryDataSet.GenreRow)lib.Genre.Rows.Find(genre);
          if (genreRow == null)
          {
            // Add genre to lib data set
            genreRow = lib.Genre.AddGenreRow(genre, 0, 0, 0.0, StripNonAlphaNumeric(genre));
          }

          // Album Artist
          String albumArtist = media.AlbumArtist;
          // Determine if album artist has already been captured in lib data set
          LibraryDataSet.AlbumArtistRow albumArtistRow = (LibraryDataSet.AlbumArtistRow)lib.AlbumArtist.Rows.Find(albumArtist);
          if (albumArtistRow == null)
          {
            // Add album artist to lib data set
            albumArtistRow = lib.AlbumArtist.AddAlbumArtistRow(albumArtist, 0, 0, 0.0, StripNonAlphaNumeric(albumArtist));
          }

          // Album
          String albumTitle = media.Album;
          Int16 year = (Int16)media.Year;
          // Determine if album has already been captured in lib data set
          object[] objAlbumArtistAlbum = new object[] { albumArtist, albumTitle };
          LibraryDataSet.AlbumRow albumRow = (LibraryDataSet.AlbumRow)lib.Album.Rows.Find(objAlbumArtistAlbum);
          if (albumRow == null)
          {
            // Add album to lib data set
            albumRow = lib.Album.AddAlbumRow(genreRow, albumArtistRow, albumTitle, year, 0, 0.0, StripNonAlphaNumeric(albumTitle));
            // Update statistics
            albumArtistRow.numAlbums++;
            genreRow.numAlbums++;
          }

          // Track
          Int16 trackNumber = (Int16)media.TrackNumber;
          String trackName = media.Name;
          String composer = media.Composer;
          String conductor = ""; // iTunes doesn't have conductor
          String trackLocation = media.Location;
          Double duration = (Double)media.Duration;
          DateTime userLastPlayedTime = media.PlayedDate;
          // dateTimeUserLastPlayed cannot be null so set to 0 if userLastPlayedTime is null
          DateTime dateTimeUserLastPlayed = new DateTime(0);
          Int16 userPlayCount = (Int16)media.PlayedCount;
          Int32 bitrate = media.BitRate;
          DateTime addedToLibDateTime = media.DateAdded;
          LibraryDataSet.TrackRow trackRow;
          // Add track (song) to lib data set
          trackRow = lib.Track.AddTrackRow
          (
              albumArtist,
              albumTitle,
              trackNumber,
              trackName,
              composer,
              conductor,
              duration,
              trackLocation,
              dateTimeUserLastPlayed,
              userPlayCount,
              bitrate,
              addedToLibDateTime
          );
          // Update statistics
          genreRow.numTracks++;
          genreRow.totalDuration += Convert.ToDouble(duration);
          albumArtistRow.numTracks++;
          albumArtistRow.totalDuration += Convert.ToDouble(duration);
          albumRow.totalDuration += Convert.ToDouble(duration);
          albumRow.numTracks++;
          totalDuration += Convert.ToDouble(duration);
        }

        // Display progress
        if (i % 100 == 0)
        {
          Console.Write("*");
        }
        i++;
      }
      Console.WriteLine("");
    }  // function FillLib

    // Strip non-alphanumeric characters from string
    private static String StripNonAlphaNumeric(String text)
    {
      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < text.Length; i++)
      {
        if (Char.IsLetterOrDigit(text[i]))
        {
          sb.Append(text[i]);
        }
      }
      return sb.ToString();
    }  // function StripNonAlphaNumeric

    // Display library statistics on console
    public void DisplayStats()
    {
      Program.logFile.OutputNoDateTime("Total Stats");
      Program.logFile.OutputNoDateTime(String.Format("Num Tracks = {0}", GetNumTracks()));
      Program.logFile.OutputNoDateTime(String.Format("Duration = {0} (s) {1} (d.hh:mm:ss)", Convert.ToInt32(GetTotalDuration()), SecToDayHrMinSec(GetTotalDuration())));
      Program.logFile.OutputNoDateTime("");
      Program.logFile.OutputNoDateTime("Genre Stats");
      Program.logFile.OutputNoDateTime(String.Format("Num Genres = {0}", GetNumGenres()));
      DisplayGenreStats();
      Program.logFile.OutputNoDateTime("");
      Program.logFile.OutputNoDateTime("Album Artist Stats");
      Program.logFile.OutputNoDateTime(String.Format("Num Album Artists = {0}", GetNumAlbumArtists()));
      Program.logFile.OutputNoDateTime("");
      Program.logFile.OutputNoDateTime("Album Stats");
      Program.logFile.OutputNoDateTime(String.Format("Num Albums = {0}", GetNumAlbums()));
      Program.logFile.OutputNoDateTime("");
    }  // function DisplayStats

    // Display genre statistics on console
    public void DisplayGenreStats()
    {
      // Determine max length of genre names
      int maxNameLen = 0;
      foreach (LibraryDataSet.GenreRow r in lib.Genre.Rows)
      {
        if (r.name.Length > maxNameLen)
        {
          maxNameLen = r.name.Length;
        }
      }
      // Display headers
      Program.logFile.OutputNoDateTime(String.Format("{0,-12}{1,9}{2,9} {3,12} {4,18}", "Genre", "# Albums", "# Tracks", "Duration (s)", "Duration (d.hh:mm:ss)"));
      Program.logFile.OutputNoDateTime(String.Format("{0} {1} {2} {3} {4}", "".PadLeft(12, '-'), "".PadLeft(8, '-'), "".PadLeft(8, '-'), "".PadLeft(12, '-'), "".PadLeft(21, '-')));
      // Display each genre
      foreach (LibraryDataSet.GenreRow r in (LibraryDataSet.GenreRow[])(lib.Genre.Select("name like '%'", "name asc")))
      {
        Program.logFile.OutputNoDateTime(String.Format("{0,-12}{1,9}{2,9} {3,12} {4,21}", r.name, r.numAlbums, r.numTracks, Convert.ToInt32(r.totalDuration), SecToDayHrMinSec(r.totalDuration)));
      }
    }  // function DisplayGenreStats

    // Return number of genres
    public Int32 GetNumGenres()
    {
      return lib.Genre.Count;
    }  // function GetNumGenres

    // Return number of album artists
    public Int32 GetNumAlbumArtists()
    {
      return lib.AlbumArtist.Count;
    }  // function GetNumAlbumArtists

    // Return number of albums (discs)
    public Int32 GetNumAlbums()
    {
      return lib.Album.Count;
    }  // function GetNumAlbums

    // Return number of tracks
    public Int32 GetNumTracks()
    {
      return lib.Track.Count;
    }  // function GetNumTracks

    // Return duration in seconds
    public Double GetTotalDuration()
    {
      return totalDuration;
    }  // function GetTotalDuration

    // Convert seconds to formatted string in d.hh:mm:ss form
    private String SecToDayHrMinSec(Double fduration)
    {
      Int32 day;
      Int32 hr;
      Int32 min;
      Int32 sec;
      String dayStr;
      String hrStr;
      String minStr;
      String secStr;
      const Int32 SecPerDay = 60 * 60 * 24;
      const Int32 SecPerHr = 60 * 60;
      const Int32 SecPerMin = 60;

      sec = Convert.ToInt32(fduration);
      day = sec / SecPerDay;
      sec -= day * SecPerDay;
      hr = sec / SecPerHr;
      sec -= hr * SecPerHr;
      min = sec / SecPerMin;
      sec -= min * SecPerMin;
      dayStr = day.ToString();
      hrStr = hr.ToString("D2");
      minStr = min.ToString("D2");
      secStr = sec.ToString("D2");

      //            return dayStr.PadLeft(3) + "d " + hrStr.PadLeft(2) + "h " + minStr.PadLeft(2) + "m " + secStr.PadLeft(2) + "s";
      return String.Format("{0,3}.{1,2}:{2,2}:{3,2}", dayStr, hrStr, minStr, secStr);
    }  // function SecToDayHrMinSec

    // Write lib data set to XML file
    public void WriteLibXML()
    {
      Program.logFile.Output(String.Format("Writing library data set to {0}", Properties.Settings.Default.LibraryXMLFileName));
      lib.WriteXml(Properties.Settings.Default.LibraryXMLFileName);
    }  // function WriteLibXML

    // Write lib schema file
    public void WriteLibSchema()
    {
      Program.logFile.Output(String.Format("Writing library schema to {0}", Properties.Settings.Default.LibrarySchemaFileName));
      lib.WriteXmlSchema(Properties.Settings.Default.LibrarySchemaFileName);
    }  // function WriteLibSchema

    // Populate lib data set from XML file
    public void ReadLibXML()
    {
      Program.logFile.Output(String.Format("Reading library data set from {0}", Properties.Settings.Default.LibraryXMLFileName));
      lib.ReadXml(Properties.Settings.Default.LibraryXMLFileName);
      totalDuration = 0;
      // Update total duration statistic
      foreach (LibraryDataSet.TrackRow r in lib.Track.Rows)
      {
        totalDuration += r.duration;
      }
    }  // function ReadLibXML

  }  // class MusicLibrary

  class LogFile
  {

    private static TextWriter logFileTW;  // text stream for log file

    public LogFile()
    {
      logFileTW = new StreamWriter(Properties.Settings.Default.LogFileName, true);
    }  // constructor LogFile

    // Write output to Log file and Console
    public void Output(string message)
    {
      string dateTime = DateTime.Now.ToString();
      logFileTW.WriteLine("{0} {1}", dateTime, message);
      logFileTW.Flush();
      Console.WriteLine("{0} {1}", dateTime, message);
    }  // function Output

    public void OutputNoDateTime(string message)
    {
      logFileTW.WriteLine(message);
      logFileTW.Flush();
      Console.WriteLine(message);
    }  // function OutputNoDateTime

    public void OutputMajorSeparator()
    {
      string temp = "".PadLeft(70, '=');
      logFileTW.WriteLine(temp);
      Console.WriteLine(temp);
    }  // function OutputMajorSeparator

    public void OutputMinorSeparator()
    {
      string temp = "".PadLeft(70, '-');
      logFileTW.WriteLine(temp);
      Console.WriteLine(temp);
    }  // function OutputMinorSeparator

    public void Close()
    {
      logFileTW.Close();
    }  // function Close

  }  // class LogFile

}  // namespace iTunesLib
