#!/usr/bin/env python3
"""Convert IBKR API HTML docs to clean markdown."""

import re
import sys
from bs4 import BeautifulSoup, Comment, Tag
from markdownify import markdownify as md

def convert(html_path: str, md_path: str) -> None:
    with open(html_path, "r", encoding="utf-8") as f:
        html = f.read()

    soup = BeautifulSoup(html, "html.parser")

    # Collect all api-block sections in order
    sections = soup.find_all("section")
    api_sections = [s for s in sections
                    if any("api-block" in c for c in s.get("class", []))]

    print(f"Found {len(api_sections)} API sections")

    # Build a new soup with just the API content
    combined = BeautifulSoup("<div></div>", "html.parser")
    container = combined.find("div")

    for section in api_sections:
        # Extract to avoid parent interference
        section_copy = section.extract()
        container.append(section_copy)

    # Clean within the combined content
    for tag in container.find_all(["script", "style", "svg", "link", "meta",
                                    "noscript", "iframe", "button", "form", "input"]):
        tag.decompose()

    for comment in container.find_all(string=lambda text: isinstance(text, Comment)):
        comment.extract()

    # Remove all presentation attributes
    for tag in container.find_all(True):
        attrs_to_remove = [attr for attr in tag.attrs
                          if attr not in ("href", "src", "colspan", "rowspan")]
        for attr in attrs_to_remove:
            del tag[attr]

    # Convert to markdown
    markdown = md(str(container), heading_style="ATX", bullets="-",
                  strip=["img"], code_language="")

    # Clean up
    # Remove "Copy Location" text
    markdown = re.sub(r"Copy Location\s*", "", markdown)

    # Collapse excessive blank lines
    markdown = re.sub(r"\n{4,}", "\n\n\n", markdown)

    # Remove leading/trailing whitespace on lines
    lines = markdown.split("\n")
    cleaned = []
    for line in lines:
        cleaned.append(line.rstrip())

    result = "\n".join(cleaned).strip() + "\n"

    with open(md_path, "w", encoding="utf-8") as f:
        f.write(result)

    print(f"Converted {html_path} -> {md_path}")
    print(f"Output: {len(result)} chars, {result.count(chr(10))} lines")

if __name__ == "__main__":
    convert(sys.argv[1], sys.argv[2])
