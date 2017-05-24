﻿using System;
using System.Collections.Generic;
using Mond;
using Mond.Binding;
using Mond.Libraries;
using Rant;

namespace MondHost
{
    class RantLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new RantLibrary();
        }
    }

    class RantLibrary : IMondLibrary
    {
        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var rantModule = MondModuleBinder.Bind(typeof(RantModule));
            yield return new KeyValuePair<string, MondValue>("Rant", rantModule);
        }
    }

    [MondModule("Rant")]
    static class RantModule
    {
        private static RantEngine _rant;

        static RantModule()
        {
            _rant = new RantEngine();
            _rant.LoadPackage("Rantionary-3.0.17.rantpkg");
        }

        [MondFunction("run")]
        public static string Run(string pattern)
        {
            try
            {
                var program = RantProgram.CompileString(pattern);
                var result = _rant.Do(program, 5000, 2.5);
                return result.Main;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}