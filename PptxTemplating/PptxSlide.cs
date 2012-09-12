﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace PptxTemplating
{
    class PptxSlide
    {
        private readonly SlidePart _slide;

        public PptxSlide(SlidePart slide)
        {
            _slide = slide;
        }

        /// Returns all text found inside the slide.
        /// See How to: Get All the Text in a Slide in a Presentation http://msdn.microsoft.com/en-us/library/office/cc850836
        public string[] GetAllText()
        {
            // Create a new linked list of strings.
            LinkedList<string> texts = new LinkedList<string>();

            // Iterate through all the paragraphs in the slide.
            foreach (A.Paragraph p in _slide.Slide.Descendants<A.Paragraph>())
            {
                StringBuilder paragraphText = new StringBuilder();

                // Iterate through the lines of the paragraph.
                foreach (A.Text t in p.Descendants<A.Text>())
                {
                    paragraphText.Append(t.Text);
                }

                if (paragraphText.Length > 0)
                {
                    texts.AddLast(paragraphText.ToString());
                }
            }

            return texts.ToArray();
        }

        class RunIndex
        {
            public A.Run Run { get; set; }
            public A.Text Text { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get { return StartIndex + Text.Text.Length; } }

            public RunIndex(A.Run r, A.Text t, int startIndex)
            {
                Run = r;
                Text = t;
                StartIndex = startIndex;
            }
        }

        /// Replaces a text (tag) by another inside the slide.
        /// See How to replace a paragraph's text using OpenXML SDK http://stackoverflow.com/questions/4276077/how-to-replace-an-paragraphs-text-using-openxml-sdk
        public void ReplaceTag(string tag, string newText)
        {
            /*
             <a:p>
              <a:r>
               <a:rPr lang="en-US" dirty="0" smtClean="0"/>
               <a:t>
                Hello this is a tag: {{hello}}
               </a:t>
              </a:r>
              <a:endParaRPr lang="fr-FR" dirty="0"/>
             </a:p>
            */

            /*
             <a:p>
              <a:r>
               <a:rPr lang="en-US" dirty="0" smtClean="0"/>
               <a:t>
                Another tag: {{bonjour
               </a:t>
              </a:r>
              <a:r>
               <a:rPr lang="en-US" dirty="0" smtClean="0"/>
               <a:t>
                }} le monde !
               </a:t>
              </a:r>
              <a:endParaRPr lang="en-US" dirty="0"/>
             </a:p>
            */

            foreach (A.Paragraph p in _slide.Slide.Descendants<A.Paragraph>())
            {
                StringBuilder concat = new StringBuilder();
                List<RunIndex> runs = new List<RunIndex>();

                // Concats all a:t
                foreach (A.Run r in p.Descendants<A.Run>())
                {
                    foreach (A.Text t in r.Descendants<A.Text>())
                    {
                        runs.Add(new RunIndex(r, t, concat.Length));

                        string tmp = t.Text;
                        concat.Append(tmp);
                    }
                }
                //

                string fullText = concat.ToString();

                // Search for the tag
                MatchCollection matches = Regex.Matches(fullText, tag);
                foreach (Match match in matches)
                {
                    //foreach (RunIndex run in runs)
                    for (int i = 0; i < runs.Count; i++)
                    {
                        RunIndex run = runs[i];
                        if (match.Index >= run.StartIndex && match.Index <= run.EndIndex)
                        {
                            // Ok we got the right a:r/a:t

                            int index = match.Index;
                            int done = 0;
                            for (; i < runs.Count; i++)
                            {
                                RunIndex currentRun = runs[i];

                                List<char> currentRunText = new List<char>(currentRun.Text.Text.ToCharArray());

                                for (int k = index; k < currentRunText.Count; k++, done++)
                                {
                                    if (done < newText.Length)
                                    {
                                        if (done >= tag.Length - 1)
                                        {
                                            // Case if newText is longer than the tag
                                            // Insert characters
                                            int remains = newText.Length - done;
                                            currentRunText.RemoveAt(k);
                                            currentRunText.InsertRange(k, newText.Substring(done, remains));
                                            done += remains;
                                            break;
                                        }
                                        else
                                        {
                                            currentRunText[k] = newText[done];
                                        }
                                    }
                                    else
                                    {
                                        if (done < tag.Length)
                                        {
                                            // Case if newText is shorter than the tag
                                            // Erase characters
                                            int remains = tag.Length - done;
                                            if (remains > currentRunText.Count - k)
                                            {
                                                remains = currentRunText.Count - k;
                                            }
                                            currentRunText.RemoveRange(k, remains);
                                            done += remains;
                                            break;
                                        }
                                        else
                                        {
                                            // Regular case, nothing to do
                                            //currentRunText[k] = currentRunText[k];
                                        }
                                    }
                                }
                                currentRun.Text.Text = new string(currentRunText.ToArray());
                                index = 0;
                            }

                            // Leave the list of a:r
                            //break;
                        }
                    }
                }
            }
        }

        /// <a:p xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
        ///   <a:r>
        ///     <a:rPr lang="en-US" dirty="0" smtClean="0" />
        ///     <a:t>Some text</a:t>
        ///   </a:r>
        ///   [...]
        /// </a:p>
        private static void InsertTextInsideParagraph(A.Paragraph p, A.Run rAfter, string text)
        {
            A.Run r = new A.Run();
            //A.RunProperties rPr = new A.RunProperties(/*rPrTemplate*/);
            A.RunProperties rPr = new A.RunProperties() { Language = "fr-FR", Dirty = false, SmartTagClean = false };
            A.Text t = new A.Text(text);

            r.AppendChild(rPr);
            //r.AppendChild(rPr);
            r.AppendChild(t);

            //p.AppendChild(r);
            p.InsertAfter(r, rAfter);
        }

        private static void InsertTextInsideParagraph(A.Paragraph p, int at, string text)
        {
            A.Run r = new A.Run();
            //A.RunProperties rPr = new A.RunProperties(/*rPrTemplate*/);
            A.RunProperties rPr = new A.RunProperties() { Language = "fr-FR", Dirty = false, SmartTagClean = false };
            A.Text t = new A.Text(text);

            r.AppendChild(rPr);
            //r.AppendChild(rPr);
            r.AppendChild(t);

            //p.AppendChild(r);
            p.InsertAt(r, at);
        }
    }
}
