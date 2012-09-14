﻿namespace PptxTemplater
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using Picture = DocumentFormat.OpenXml.Presentation.Picture;

    /// <summary>
    /// Represents a slide inside a PowerPoint file.
    /// </summary>
    class PptxSlide
    {
        private readonly SlidePart slidePart;

        public PptxSlide(SlidePart slidePart)
        {
            this.slidePart = slidePart;
        }

        /// <summary>
        /// Gets all text found inside the slide.
        /// </summary>
        /// <remarks>
        /// Some strings inside the array can be empty, this happens when all A.Text from a paragraph are empty
        /// <see href="http://msdn.microsoft.com/en-us/library/office/cc850836">How to: Get All the Text in a Slide in a Presentation</see>
        /// </remarks>
        public string[] GetAllText()
        {
            List<string> texts = new List<string>();
            foreach (A.Paragraph p in this.slidePart.Slide.Descendants<A.Paragraph>())
            {
                texts.Add(GetParagraphAllText(p));
            }
            return texts.ToArray();
        }

        /// <summary>
        /// Returns all text found inside a given paragraph.
        /// </summary>
        /// <remarks>
        /// If all A.Text in the given paragraph are empty, returns an empty string
        /// </remarks>
        private static string GetParagraphAllText(A.Paragraph p)
        {
            StringBuilder concat = new StringBuilder();
            foreach (A.Text t in p.Descendants<A.Text>())
            {
                concat.Append(t.Text);
            }
            return concat.ToString();
        }

        /// <summary>
        /// Associates a A.Text with start and end index matching a paragraph full string (= the concatenation of all A.Text of a paragraph).
        /// </summary>
        private class TextIndex
        {
            public A.Text Text { get; private set; }
            public int StartIndex { get; private set; }
            public int EndIndex { get { return StartIndex + Text.Text.Length; } }

            public TextIndex(A.Text t, int startIndex)
            {
                this.Text = t;
                this.StartIndex = startIndex;
            }
        }

        /// <summary>
        /// Gets all the TextIndex for a given paragraph.
        /// </summary>
        private static List<TextIndex> GetTextIndexList(A.Paragraph p)
        {
            List<TextIndex> texts = new List<TextIndex>();

            StringBuilder concat = new StringBuilder();
            foreach (A.Text t in p.Descendants<A.Text>())
            {
                int startIndex = concat.Length;
                texts.Add(new TextIndex(t, startIndex));
                concat.Append(t.Text);
            }

            return texts;
        }

        /// <summary>
        /// Replaces a text (tag) by another inside the slide.
        /// </summary>
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

            foreach (A.Paragraph p in this.slidePart.Slide.Descendants<A.Paragraph>())
            {
                while (true)
                {
                    string allText = GetParagraphAllText(p);

                    // Search for the tag
                    Match match = Regex.Match(allText, tag);
                    if (!match.Success)
                    {
                        break;
                    }

                    List<TextIndex> texts = GetTextIndexList(p);

                    for (int i = 0; i < texts.Count; i++)
                    {
                        TextIndex text = texts[i];
                        if (match.Index >= text.StartIndex && match.Index <= text.EndIndex)
                        {
                            // Got the right A.Text

                            int index = match.Index - text.StartIndex;
                            int done = 0;

                            for (; i < texts.Count; i++)
                            {
                                TextIndex currentText = texts[i];
                                List<char> currentTextChars = new List<char>(currentText.Text.Text.ToCharArray());

                                for (int k = index; k < currentTextChars.Count; k++, done++)
                                {
                                    if (done < newText.Length)
                                    {
                                        if (done >= tag.Length - 1)
                                        {
                                            // Case if newText is longer than the tag
                                            // Insert characters
                                            int remains = newText.Length - done;
                                            currentTextChars.RemoveAt(k);
                                            currentTextChars.InsertRange(k, newText.Substring(done, remains));
                                            done += remains;
                                            break;
                                        }
                                        else
                                        {
                                            currentTextChars[k] = newText[done];
                                        }
                                    }
                                    else
                                    {
                                        if (done < tag.Length)
                                        {
                                            // Case if newText is shorter than the tag
                                            // Erase characters
                                            int remains = tag.Length - done;
                                            if (remains > currentTextChars.Count - k)
                                            {
                                                remains = currentTextChars.Count - k;
                                            }
                                            currentTextChars.RemoveRange(k, remains);
                                            done += remains;
                                            break;
                                        }
                                        else
                                        {
                                            // Regular case, nothing to do
                                            //currentTextChars[k] = currentTextChars[k];
                                        }
                                    }
                                }

                                currentText.Text.Text = new string(currentTextChars.ToArray());
                                index = 0;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Replaces a picture by another inside the slide.
        /// </summary>
        /// <remarks>
        /// <see href="http://stackoverflow.com/questions/7070074/how-can-i-retrieve-images-from-a-pptx-file-using-ms-open-xml-sdk">How can I retrieve images from a .pptx file using MS Open XML SDK?</see>
        /// <see href="http://stackoverflow.com/questions/7137144/how-can-i-retrieve-some-image-data-and-format-using-ms-open-xml-sdk">How can I retrieve some image data and format using MS Open XML SDK?</see>
        /// <see href="http://msdn.microsoft.com/en-us/library/office/bb497430.aspx">How to: Insert a Picture into a Word Processing Document</see>
        /// </remarks>
        public void ReplacePicture(string tag, Stream newPicture, string contentType)
        {
            // FIXME The content type ("image/png", "image/bmp" or "image/jpeg") does not work
            // All files inside the media directory are suffixed with .bin
            // Instead if DocumentFormat.OpenXml.Packaging.ImagePartType is used, files are suffixed with the right extension
            // but I don't want to expose DocumentFormat.OpenXml.Packaging.ImagePartType to the outside world nor
            // want to add boilerplate code if ... else if ... else if ...
            // OpenXML SDK should be fixed and handle "image/png" and friends properly
            ImagePart imagePart = this.slidePart.AddImagePart(contentType);

            // FeedData() closes the stream and we cannot reuse it (ObjectDisposedException)
            // solution: copy the original stream to a MemoryStream
            using (MemoryStream stream = new MemoryStream())
            {
                newPicture.Position = 0;
                newPicture.CopyTo(stream);
                stream.Position = 0;
                imagePart.FeedData(stream);
            }

            // No need to detect duplicated images
            // PowerPoint do it for us on the next manual save

            foreach (Picture pic in this.slidePart.Slide.Descendants<Picture>())
            {
                string xml = pic.NonVisualPictureProperties.OuterXml;

                if (xml.Contains(tag))
                {
                    // Gets the relationship ID of the part
                    string rId = this.slidePart.GetIdOfPart(imagePart);

                    pic.BlipFill.Blip.Embed.Value = rId;
                }
            }
        }
    }
}
