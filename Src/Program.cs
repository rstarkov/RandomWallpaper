using System.Reflection;
using System.Runtime.InteropServices;
using RT.CommandLine;
using RT.PostBuild;
using RT.Serialization;
using RT.Util;
using RT.Util.Consoles;
using RT.Util.ExtensionMethods;

namespace RandomWallpaper
{
    class Program
    {
        static Settings Settings;
        static CommandLine Args;
        const int Success = 0;
        const int ErrorArgs = 1;
        const int ErrorUser = 2;
        const int ErrorNoImages = 3;
        const int ErrorCrash = 9;

        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--post-build-check")
                return PostBuildChecker.RunPostBuildChecks(args[1], Assembly.GetExecutingAssembly());
            SetProcessDPIAware();

            var settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RandomWallpaper", "RandomWallpaper.Settings.xml");
            if (File.Exists(settingsFile))
                Settings = ClassifyXml.DeserializeFile<Settings>(settingsFile);
            else
                Settings = new();

            Args = CommandLineParser.ParseOrWriteUsageToConsole<CommandLine>(args, helpProcessor: expandHelpTokens);
            if (Args == null)
                return ErrorArgs;

            int result;
            try
            {
                result = Args.Execute(Args);
            }
            catch (TellUserException e)
            {
                ConsoleUtil.WriteLine(e.Message);
                result = e.ExitCode;
            }
#if !DEBUG
            catch (Exception e)
            {
                ConsoleUtil.WriteLine("Error: ".Color(ConsoleColor.Red) + e.Message);
                return ErrorCrash;
            }
#endif
            try { ClassifyXml.SerializeToFile(Settings, settingsFile); }
            catch (Exception e)
            {
                ConsoleUtil.WriteLine(colorize("{red}Error:{} could not save settings. " + e.Message));
                return ErrorCrash;
            }
            return result;
        }

        class TellUserException : Exception
        {
            public new ConsoleColoredString Message { get; private set; }
            public int ExitCode { get; private set; }
            public TellUserException(ConsoleColoredString message, int exitCode) { Message = message; ExitCode = exitCode; }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetProcessDPIAware();

        private static ConsoleColoredString expandHelpTokens(ConsoleColoredString str)
        {
            return str
                .ReplaceText("$(Version)", "v{0:000}".Fmt(Assembly.GetExecutingAssembly().GetName().Version.Major))
                .ReplaceText("$(CfgSkipRecent)", Settings.SkipRecent.ToString())
                .ReplaceText("$(CfgOldBias)", Settings.OldBias.ToString("0.0###"))
                .ReplaceText("$(CfgMinTime)", Settings.MinimumTime.ToString());
        }

        private static void printInfo(string label, ConsoleColoredString value)
        {
            ConsoleUtil.WriteLine(label.Color(ConsoleColor.White) + value);
        }

        private static ConsoleColoredString colorize(string rhoML)
        {
            return CommandLineParser.Colorize(RhoML.Parse(rhoML));
        }

        private static ImageInfo getCurrent()
        {
            var curFilename = Wallpaper.Get();
            if (curFilename == null)
            {
                printInfo("Current wallpaper: ", "none".Color(ConsoleColor.DarkGray));
                return null;
            }
            else
            {
                printInfo("Current wallpaper: ", curFilename);
                if (!Settings.Images.ContainsKey(curFilename))
                    Settings.Images[curFilename] = new ImageInfo();
                var cur = Settings.Images[curFilename];
                printInfo("Applied at: ", cur.Applied == null ? "unknown".Color(ConsoleColor.DarkGray) : cur.Applied.Value.ToString());
                printInfo("More/less: ", getMoreLessStr(cur));
                Console.WriteLine();
                return cur;
            }
        }

        private static void checkPaths()
        {
            if (Settings.Paths.Count == 0)
                throw new TellUserException(colorize("{red}Error:{} please configure at least one path containing wallpapers using {command}config{} {option}--paths{}."), ErrorUser);
        }

        public static int ExecuteNext(CommandLine args, NextCmd cmd, ImageInfo cur = null)
        {
            cmd.Paths = cmd.Paths ?? Settings.Paths.ToArray();
            cmd.SkipRecent = cmd.SkipRecent ?? Settings.SkipRecent;
            cmd.OldBias = cmd.OldBias ?? Settings.OldBias;

            if (cmd.Paths != null && cmd.Paths.Length == 0)
                throw new TellUserException(colorize("{red}Error:{} please specify at least one wallpaper path using {command}next{} {option}--paths{}, or configure one permanently using {command}config{} {option}--paths{}."), ErrorUser);

            if (cur == null)
                cur = getCurrent();
            if (cur != null)
            {
                // Skip execution if it's not been there long enough (if requested)
                if (cmd.Scheduled && cur.Applied != null && (DateTime.UtcNow - cur.Applied.Value < TimeSpan.FromMinutes(Settings.MinimumTime)))
                {
                    ConsoleUtil.WriteLine(colorize("Leaving current image unchanged because it has been visible for less than {0} minutes (use {command}config{} {option}--min-time{} to configure).".Fmt(Settings.MinimumTime)));
                    return Success;
                }
                // Occasionally skip execution if the image is set to More
                if (cmd.Scheduled && !cmd.Uniform && Rnd.NextDouble() < cur.MoreOrLess)
                {
                    ConsoleUtil.WriteLine(colorize("Leaving current image unchanged because you've requested to see it more (use {command}more{}/{command}less{} to configure)."));
                    return Success;
                }
                // Save the removed timestamp
                if (!cmd.NotShown)
                    cur.Removed = DateTime.UtcNow;
            }

            // Scan for images
            var files = new List<string>();
            foreach (var path in cmd.Paths)
            {
                try { File.Exists(path); Directory.Exists(path); }
                catch
                {
                    // The path is not valid as a file/directory name. It could be because it contains a mask.
                    FileInfo[] matches;
                    try { matches = new DirectoryInfo(Path.GetDirectoryName(path)).GetFiles(Path.GetFileName(path)); }
                    catch
                    {
                        // The path is not in the form of a directory name + file mask. Error out.
                        throw new TellUserException(colorize("{red}Error:{} this path is either invalid or unsupported: {h}{0}{}".Fmt(path)), ErrorUser);
                    }
                    // The path is valid as a mask. Process it as such.
                    foreach (var match in matches)
                        files.Add(match.FullName);
                    if (matches.Length == 0)
                        ConsoleUtil.WriteLine(colorize("{yellow}Warning:{} no images matched by filter: {h}{0}{}".Fmt(path)));
                    continue;
                }

                // The path is valid as a file or directory name. Process it based on whether a directory or file exists at this path.
                if (Directory.Exists(path))
                {
                    bool any = false;
                    foreach (var f in new DirectoryInfo(path).GetFiles())
                        if (f.Name.EndsWith(".jpg") || f.Name.EndsWith(".jpeg") || f.Name.EndsWith(".png"))
                        {
                            files.Add(f.FullName);
                            any = true;
                        }
                    if (!any)
                        ConsoleUtil.WriteLine(colorize("{yellow}Warning:{} no images found at this path: {h}{0}{}".Fmt(path)));
                    continue;
                }
                else if (File.Exists(path))
                    files.Add(path);
                else
                    ConsoleUtil.WriteLine(colorize("{yellow}Warning:{} no such path: {h}{0}{}".Fmt(path)));
            }
            if (files.Count == 0)
                throw new TellUserException(colorize("{red}Error:{} there are no images to choose from."), ErrorNoImages);

            // Select next image
            again: ;
            var infos = files.Select(f => Settings.Images.ContainsKey(f) ? Settings.Images[f] : new ImageInfo { FileName = f }).ToList();
            var oldestDate = infos.Where(ii => ii.Removed != null).MinOrDefault(ii => ii.Removed.Value, DateTime.UtcNow);
            oldestDate = DateTime.UtcNow - TimeSpan.FromSeconds((DateTime.UtcNow - oldestDate).TotalSeconds * (1 + cmd.OldBias.Value)); // make new images (1+OldBias) times older than the oldest image actually shown
            infos = infos.OrderBy(ii => ii.Removed ?? oldestDate).ToList();
            int takeCount = (int)Math.Ceiling(infos.Count * (100 - cmd.SkipRecent.Value) / 100.0);
            infos = infos.Take(takeCount).Concat(infos.Skip(takeCount).Where(ii => ii.Removed == null)).ToList();
            double totalProbability = 0;
            foreach (var info in infos)
            {
                info.Probability = 0.001 + (DateTime.UtcNow - (info.Removed ?? oldestDate)).TotalSeconds * cmd.OldBias.Value;
                totalProbability += info.Probability;
            }
            double roll = Rnd.NextDouble(0, totalProbability);
            totalProbability = 0;
            int sel = 0;
            for (; sel < infos.Count; sel++)
            {
                totalProbability += infos[sel].Probability;
                if (roll <= totalProbability)
                    break;
            }
            var selected = infos[sel];
            Settings.Images[selected.FileName] = selected; // images are only added to this collection the first time each one is actually set as the wallpaper, and not before that.
            // Possibly re-select if this image has a "less" on it
            if (!cmd.Uniform && selected.MoreOrLess < 0 && Rnd.NextDouble() < -selected.MoreOrLess)
            {
                // If we just skip it then the old-bias will still cause it to be selected a lot, so it would have to be less'd massively to really reduce the frequency.
                // So instead record it as if it's been displayed.
                Console.WriteLine(colorize("Skipping {h}{0}{} because it was configured to be shown less frequently.".Fmt(selected.FileName)));
                selected.Applied = selected.Removed = DateTime.UtcNow;
                goto again;
            }

            // Apply it
            selected.Applied = DateTime.UtcNow;
            Wallpaper.Set(selected.FileName);
            Console.WriteLine(colorize("Applied next wallpaper: {h}{0}{}.".Fmt(selected.FileName)));
            return Success;
        }

        public static int ExecuteLess(CommandLine args, LessCmd cmd)
        {
            checkPaths();
            var cur = getCurrent();
            if (cur == null)
                throw new TellUserException("Cannot execute command because no current wallpaper has been detected.", ErrorUser);
            cur.MoreOrLess = cur.MoreOrLess >= 0 ? scaleMoreLess(cur.MoreOrLess, 1 / 0.8) : -scaleMoreLess(-cur.MoreOrLess, 0.8);
            printInfo("New more/less: ", getMoreLessStr(cur));
            Console.WriteLine();
            return ExecuteNext(args, new NextCmd(), cur);
        }

        public static int ExecuteMore(CommandLine args, MoreCmd cmd)
        {
            checkPaths();
            var cur = getCurrent();
            if (cur == null)
                throw new TellUserException("Cannot execute command because no current wallpaper has been detected.", ErrorUser);
            cur.MoreOrLess = cur.MoreOrLess >= 0 ? scaleMoreLess(cur.MoreOrLess, 0.8) : -scaleMoreLess(-cur.MoreOrLess, 1 / 0.8);
            printInfo("New more/less: ", getMoreLessStr(cur));
            return Success;
        }

        private static ConsoleColoredString getMoreLessStr(ImageInfo info)
        {
            return info.MoreOrLess == 0 ? "default" : ((info.MoreOrLess > 0 ? "more: " : "less: ") + "{0:0}%".Fmt(Math.Abs(info.MoreOrLess) * 100));
        }

        private static double scaleMoreLess(double moreless, double scale)
        {
            moreless = 1 - (1 - moreless) * scale; // 0, 0.20, 0.36, 0.49, 0.59 etc
            return moreless < 0.1 ? 0 : moreless;
        }

        public static int ExecuteConfig(CommandLine args, ConfigCmd cmd)
        {
            throw new NotImplementedException();
        }

        public static int ExecuteExplore(CommandLine args, ExploreCmd exploreCmd)
        {
            checkPaths();
            foreach (var path in Settings.Paths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        CommandRunner.Run("explorer", path).OutputNothing().FailExitCodes().Go();
                        ConsoleUtil.WriteLine(colorize("Opened folder {h}{0}{}.".Fmt(path)));
                        continue;
                    }
                }
                catch { }
                ConsoleUtil.WriteLine(colorize("Skipped folder {h}{0}{}: does not exist or not a directory path.".Fmt(path)));
            }
            return Success;
        }
    }
}
