using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Converts an exception stack to a sarif code location list.
        /// </summary>
        public static IList<AnnotatedCodeLocation> ToCodeLocations(this Exception exception)
        {
            List<AnnotatedCodeLocation> codeLocations = new List<AnnotatedCodeLocation>();

            StackTrace stack = new StackTrace(exception);
            foreach (StackFrame frame in stack.GetFrames())
            {
                AnnotatedCodeLocation codeLocation = new AnnotatedCodeLocation();
                MemberInfo member = frame.GetMethod();
                if (member != null)
                {
                    codeLocation.Message = member.ReflectedType.FullName + "." + member.Name;
                }

                PhysicalLocationComponent physicalLocation = new PhysicalLocationComponent();
                string filename = frame.GetFileName();
                if (!String.IsNullOrWhiteSpace(filename))
                {
                    physicalLocation.Uri = new Uri(filename);
                }
                physicalLocation.Region = new Region();
                physicalLocation.Region.StartLine = frame.GetFileLineNumber();
                physicalLocation.Region.EndLine = frame.GetFileLineNumber();
                physicalLocation.Region.StartColumn = frame.GetFileColumnNumber();
                physicalLocation.Region.EndColumn = frame.GetFileColumnNumber();

                codeLocation.PhysicalLocation = new List<PhysicalLocationComponent>() { physicalLocation };
                codeLocations.Add(codeLocation);
            }

            return codeLocations;
        }
    }
}
