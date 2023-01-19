using RT.CommandLine;
using RT.PostBuild;
using RT.Util;
using RT.Util.Consoles;

namespace RandomWallpaper
{
    [CommandLine]
    [DocumentationRhoML("{h}Desktop Wallpaper Changer{}\nVersion $(Version)\n\nRandomly selects and assigns a new desktop wallpaper from a folder of images, taking care not to select recently selected images too often, and offering options to show specific images more or less frequently than others. Does not stay resident and so only applies a change when invoked, whether manually or by the Task Scheduler.")]
    abstract class CommandLine
    {
        public abstract int Execute(CommandLine args);

#if DEBUGCONSOLE || DEBUGWINDOWLESS
        private static void PostBuildCheck(IPostBuildReporter rep)
        {
            CommandLineParser.PostBuildStep<CommandLine>(rep, null);
        }
#endif
    }

    [CommandName("next", "n")]
    [DocumentationRhoML("Picks a new image and assigns it as the desktop wallpaper.")]
    class NextCmd : CommandLine, ICommandLineValidatable
    {
        [Option("--scheduled")]
        [DocumentationRhoML("{h}Specifies that this is a scheduled invocation.{}\nUse when invoking this command from the Task Scheduler. This switch enables {option}--min-time{} and {command}more{} to take effect by possibly exiting without doing anything.")]
        public bool Scheduled = false;

        [Option("-ns", "--not-shown")]
        [DocumentationRhoML("{h}Do not record the current image as shown.{}\nBy default, the current image is saved as “recently shown”, which typically makes it less likely to appear again for some time (depending on settings).")]
        public bool NotShown = false;

        [Option("-u", "--uniform")]
        [DocumentationRhoML("{h}Ignores the {command}less{}/{command}more{} tweaks just this time{}.")]
        public bool Uniform = false;

        [Option("-sr", "--skip-recent")]
        [DocumentationRhoML("{h}How many of the most recently selected wallpapers to skip, as % of all images.{}\nSame as the corresponding {command}config{} option, but has effect only once, and is not saved for the next time.\n{darkgray}If omitted, will use the configured value: $(CfgSkipRecent).{}")]
        public int? SkipRecent = null;

        [Option("-ob", "--old-bias")]
        [DocumentationRhoML("{h}Skews the random selection towards older images.{}\n\nSame as the corresponding {command}config{} option, but has effect only once, and is not saved for the next time.\n{darkgray}If omitted, will use the configured value: $(CfgOldBias).{}")]
        public double? OldBias = null;

        [Option("-p", "--path", "--paths")]
        [DocumentationRhoML("{h}One or more paths from which to select the images.{}\nSame as the corresponding {command}config{} option, but has effect only once, and is not saved for the next time.")]
        public string[] Paths = null;

        public override int Execute(CommandLine args)
        {
            return Program.ExecuteNext(args, this);
        }

        public ConsoleColoredString Validate()
        {
            if (SkipRecent != null && (SkipRecent < 0 || SkipRecent > 99))
                return CommandLineParser.Colorize(RhoML.Parse("The value for {option}--skip-recent{} must be between 0 and 99."));
            if (OldBias != null && (OldBias < 0))
                return CommandLineParser.Colorize(RhoML.Parse("The value for {option}--old-bias{} must be zero or greater."));
            return null;
        }
    }

    [CommandName("less", "l")]
    [DocumentationRhoML("{h}Makes the current image less likely to appear again, and switches to the next image using {command}next{}.{}\nWhenever an image is chosen as the next wallpaper, there is a probability P (initially 100) that the image will actually be used (instead of marking it as shown and skipped). This command reduces that probability by 20%.\n{darkgray}For images that were previously subject to {command}more{}, this command undoes one invocation of {command}more{} instead.{}")]
    class LessCmd : CommandLine
    {
        public override int Execute(CommandLine args)
        {
            return Program.ExecuteLess(args, this);
        }
    }

    [CommandName("more", "m")]
    [DocumentationRhoML("{h}Makes the current image more likely to appear again.{}\nWhenever the {command}next{} command runs, there is a probability P (initially 100) that the wallpaper will actually be changed (instead of keeping the current image). This command reduces that probability by 20% whenever the current image is active.\n{darkgray}For images that were previously subject to {command}less{}, this command undoes one invocation of {command}less{} instead.{}")]
    class MoreCmd : CommandLine
    {
        public override int Execute(CommandLine args)
        {
            return Program.ExecuteMore(args, this);
        }
    }

    [CommandName("config", "c")]
    [DocumentationRhoML("Displays and optionally changes some of the settings affecting the wallpaper selection.")]
    class ConfigCmd : CommandLine, ICommandLineValidatable
    {
        [Option("-sr", "--skip-recent")]
        [DocumentationRhoML("{h}How many of the most recently selected wallpapers to skip, as % of all images.{}\nAll the selected images are ordered by how long ago they've been last selected as the wallpaper, and {option}SkipRecent{}% of them are excluded from the selection, except if the image has never been selected.\n{darkgray}Current value: $(CfgSkipRecent).{}")]
        public int? SkipRecent = null;

        [Option("-ob", "--old-bias")]
        [DocumentationRhoML("{h}Skews the random selection towards older images.{}\nEach of the eligible images is assigned a probability of being selected that is proportional to time since it was last selected, multiplied by this bias factor. Thus 0 results in no bias (all eligible images are equally likely) and higher values make older images more likely.\n{darkgray}Current value: $(CfgOldBias).{}")]
        public double? OldBias = null;

        [Option("-mt", "--min-time")]
        [DocumentationRhoML("{h}Specifies the minimum time, in minutes, that a wallpaper should be shown.{}\nThis only has effect in conjunction with the {option}--respect-minimum{} option on {command}next{}.\n{darkgray}Current value: $(CfgMinTime).{}")]
        public int? MinimumTime = null;

        [Option("-p", "--path", "--paths")]
        [DocumentationRhoML("{h}One or more paths from which to select the images.{}\nYou may switch between different sets of paths with no adverse effects: all information about what wallpapers have been recently used remains unaffected.")]
        public string[] Paths = null;

        public override int Execute(CommandLine args)
        {
            return Program.ExecuteConfig(args, this);
        }

        public ConsoleColoredString Validate()
        {
            if (SkipRecent != null && (SkipRecent < 0 || SkipRecent > 99))
                return CommandLineParser.Colorize(RhoML.Parse("The value for {option}--skip-recent{} must be between 0 and 99."));
            if (OldBias != null && (OldBias < 0))
                return CommandLineParser.Colorize(RhoML.Parse("The value for {option}--old-bias{} must be zero or greater."));
            if (MinimumTime != null && (MinimumTime < 0))
                return CommandLineParser.Colorize(RhoML.Parse("The value for {option}--min-time{} must be zero or greater."));
            return null;
        }
    }

    [CommandName("explore", "e")]
    [DocumentationRhoML("Opens an explorer window for every configured wallpaper folder.")]
    class ExploreCmd : CommandLine
    {
        public override int Execute(CommandLine args)
        {
            return Program.ExecuteExplore(args, this);
        }
    }
}
