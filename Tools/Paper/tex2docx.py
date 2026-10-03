"""Converts the EPW 17 manuscript (document.tex) into a Word document with python-docx.

Tailored to this manuscript's constructs: title block, abstract, starred sections, paragraphs with inline
formatting and math, one display equation, booktabs tables (tabularx and longtable), figures with subfigures or
pre-rendered TikZ images, and a numbered bibliography.
"""
import os, re, sys
from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_TAB_ALIGNMENT
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import qn, nsdecls
from docx.shared import Cm, Pt, RGBColor

HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Documentation", "Paper", "build")
os.makedirs(HERE, exist_ok=True)
PAPER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Documentation", "Paper") + os.sep
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "document.docx")
TEXT_W = 21.0 - 2.54 - 2.54  # cm
FONT = "Times New Roman"

tex = open(PAPER + "document.tex", encoding="utf-8").read()
body = tex[tex.index(r"\begin{document}") + len(r"\begin{document}"):tex.index(r"\end{document}")]
# Strip comments (unescaped % to end of line).
body = "\n".join(re.sub(r"(?<!\\)%.*", "", line) for line in body.split("\n"))

# ---------- numbering for \ref and \cite ----------
refs = {}
for n, m in enumerate(re.finditer(r"\\begin\{figure\}.*?\\end\{figure\}", body, re.S), 1):
    lab = re.search(r"\\label\{(fig:[^}]*)\}", m.group(0))
    if lab: refs[lab.group(1)] = str(n)
tables = [(m.start(), re.search(r"\\label\{(tab:[^}]*)\}", m.group(0)).group(1))
          for m in re.finditer(r"\\begin\{(table|longtable)\}.*?\\end\{\1\}", body, re.S)]
for n, (_, lab) in enumerate(sorted(tables), 1):
    refs[lab] = str(n)
for n, m in enumerate(re.finditer(r"\\begin\{equation\}.*?\\label\{([^}]*)\}", body, re.S), 1):
    refs[m.group(1)] = str(n)
bib_keys = re.findall(r"\\bibitem\{([^}]*)\}", body)
bib = {k: i + 1 for i, k in enumerate(bib_keys)}


def cite_text(keys):
    nums = sorted(bib[k.strip()] for k in keys.split(","))
    parts, i = [], 0
    while i < len(nums):
        j = i
        while j + 1 < len(nums) and nums[j + 1] == nums[j] + 1: j += 1
        parts.append(str(nums[i]) if j == i else (f"{nums[i]}, {nums[j]}" if j == i + 1 else f"{nums[i]}\u2013{nums[j]}"))
        i = j + 1
    return "[" + ", ".join(parts) + "]"


# ---------- document setup ----------
doc = Document()
sec = doc.sections[0]
sec.page_width, sec.page_height = Cm(21.0), Cm(29.7)
sec.top_margin, sec.left_margin, sec.right_margin, sec.bottom_margin = Cm(2.54), Cm(2.54), Cm(2.54), Cm(3.0)


def set_font(style_or_run, size=None, bold=None, italic=None):
    f = style_or_run.font
    f.name = FONT
    rpr = style_or_run.element.get_or_add_rPr() if hasattr(style_or_run.element, "get_or_add_rPr") else style_or_run.element.rPr
    rfonts = rpr.find(qn("w:rFonts"))
    if rfonts is None:
        rfonts = OxmlElement("w:rFonts"); rpr.insert(0, rfonts)
    for a in ("w:ascii", "w:hAnsi", "w:eastAsia", "w:cs"): rfonts.set(qn(a), FONT)
    for a in ("w:asciiTheme", "w:hAnsiTheme", "w:eastAsiaTheme", "w:cstheme"):  # theme fonts override explicit ones
        if rfonts.get(qn(a)) is not None: del rfonts.attrib[qn(a)]
    if size: f.size = Pt(size)
    if bold is not None: f.bold = bold
    if italic is not None: f.italic = italic
    f.color.rgb = RGBColor(0, 0, 0)


normal = doc.styles["Normal"]
set_font(normal, 10)
normal.paragraph_format.space_before = Pt(0); normal.paragraph_format.space_after = Pt(0)
normal.paragraph_format.line_spacing = 1.0
for name, size, before, after in (("Heading 1", 11, 10, 5), ("Heading 2", 10, 7, 3)):
    st = doc.styles[name]
    set_font(st, size, bold=True, italic=False)
    st.paragraph_format.space_before = Pt(before); st.paragraph_format.space_after = Pt(after)
    st.paragraph_format.keep_with_next = True

# Centred page number in the footer.
fp = sec.footer.paragraphs[0]; fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
for kind, text in (("begin", None), (None, "PAGE"), ("end", None)):
    r = fp.add_run(); set_font(r, 10)
    if kind:
        fc = OxmlElement("w:fldChar"); fc.set(qn("w:fldCharType"), kind); r._r.append(fc)
    else:
        it = OxmlElement("w:instrText"); it.set(qn("xml:space"), "preserve"); it.text = text; r._r.append(it)


# ---------- inline parsing ----------
def read_arg(s, i):
    while i < len(s) and s[i] in " \n": i += 1
    if i < len(s) and s[i] == "{":
        depth, j = 0, i
        while j < len(s):
            if s[j] == "{" and (j == 0 or s[j - 1] != "\\"): depth += 1
            elif s[j] == "}" and s[j - 1] != "\\":
                depth -= 1
                if depth == 0: return s[i + 1:j], j + 1
            j += 1
        return s[i + 1:], len(s)
    return (s[i], i + 1) if i < len(s) else ("", i)


def add_run(par, text, fmt, size):
    if not text: return
    r = par.add_run(text); set_font(r, fmt.get("size", size))
    r.bold = fmt.get("bold") or None; r.italic = fmt.get("italic") or None
    if fmt.get("mono"):
        r.font.name = "Courier New"
        r.element.rPr.find(qn("w:rFonts")).set(qn("w:ascii"), "Courier New")
        r.element.rPr.find(qn("w:rFonts")).set(qn("w:hAnsi"), "Courier New")
    if fmt.get("sup"): r.font.superscript = True
    if fmt.get("sub"): r.font.subscript = True


GREEK = {"lambda": "\u03bb", "pm": "\u00b1", "in": "\u2208", "notin": "\u2209", "qquad": "\u2003\u2003", "quad": "\u2003", ",": "\u2009",
         "{": "{", "}": "}", "times": "\u00d7", "le": "\u2264", "ge": "\u2265"}


def add_math(par, s, fmt, size):
    i = 0
    while i < len(s):
        c = s[i]
        if c == "\\":
            m = re.match(r"\\([A-Za-z]+|.)", s[i:]); name = m.group(1); i += m.end()
            if name == "mathbf":
                arg, i = read_arg(s, i); add_run(par, "\U0001d7cf" if arg == "1" else arg, {**fmt, "bold": True}, size)
            else:
                add_run(par, GREEK.get(name, ""), {**fmt, "italic": name == "lambda"}, size)
        elif c in "_^":
            arg, i = read_arg(s, i + 1)
            sub = {**fmt, "sub" if c == "_" else "sup": True}
            for ch in arg: add_run(par, ch, {**sub, "italic": ch.isalpha()}, size)
        elif c in "{}":
            i += 1
        else:
            add_run(par, c, {**fmt, "italic": c.isalpha()}, size); i += 1


def inline(par, s, fmt=None, size=10):
    fmt = fmt or {}
    s = re.sub(r"\s+", " ", s)
    buf, i = [], 0

    def flush():
        if buf: add_run(par, "".join(buf), fmt, size); buf.clear()

    while i < len(s):
        c = s[i]
        if c == "\\":
            m = re.match(r"\\([A-Za-z]+)\*?|\\(.)", s[i:]); name = m.group(1) or m.group(2); i += m.end()
            if name in ("emph", "textit", "textbf", "texttt", "textsuperscript"):
                arg, i = read_arg(s, i); flush()
                key = {"emph": "italic", "textit": "italic", "textbf": "bold", "texttt": "mono", "textsuperscript": "sup"}[name]
                inline(par, arg, {**fmt, key: (not fmt.get(key)) if key == "italic" else True}, size)
            elif name == "href":
                _, i = read_arg(s, i); txt, i = read_arg(s, i); flush(); inline(par, txt, fmt, size)
            elif name == "url":
                arg, i = read_arg(s, i); buf.append(arg)
            elif name == "cite":
                arg, i = read_arg(s, i); buf.append(cite_text(arg))
            elif name == "ref":
                arg, i = read_arg(s, i); buf.append(refs.get(arg, "??"))
            elif name == "shortstack":
                arg, i = read_arg(s, i); flush(); inline(par, arg.replace("\\\\", " "), fmt, size)
            elif name == "\\":
                flush(); par.add_run().add_break()
            elif name in ("%", "&", "_", "#", "$", "{", "}"):
                buf.append(name)
            elif name == ",":
                buf.append("\u2009")
            elif name == "ldots":
                buf.append("\u2026")
            elif name in ("vspace", "hspace", "label"):
                _, i = read_arg(s, i)
            elif name == "fontsize":
                _, i = read_arg(s, i); _, i = read_arg(s, i)
            # Layout-only commands (\noindent, \par, \centering, \raggedright, ...) are dropped.
        elif c == "$":
            j = s.index("$", i + 1); flush(); add_math(par, s[i + 1:j], fmt, size); i = j + 1
        elif c in "{}":
            i += 1
        elif c == "~":
            buf.append("\u00a0"); i += 1
        elif s.startswith("---", i):
            buf.append("\u2014"); i += 3
        elif s.startswith("--", i):
            buf.append("\u2013"); i += 2
        elif s.startswith("``", i):
            buf.append("\u201c"); i += 2
        elif s.startswith("''", i):
            buf.append("\u201d"); i += 2
        else:
            buf.append(c); i += 1
    flush()


def para(text, align=WD_ALIGN_PARAGRAPH.JUSTIFY, indent=True, size=10, spacing=None, style=None, keep=False):
    p = doc.add_paragraph(style=style)
    p.alignment = align
    pf = p.paragraph_format
    pf.first_line_indent = Cm(0.5) if indent else Cm(0)
    if spacing: pf.line_spacing = spacing
    if keep: pf.keep_with_next = True
    inline(p, text.strip(), size=size)
    return p


def caption(kind, label_key, text, keep=False):
    p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(3); p.paragraph_format.space_after = Pt(6)
    if keep: p.paragraph_format.keep_with_next = True
    add_run(p, f"{kind} {refs[label_key]}. ", {}, 10)
    inline(p, text, size=10)


# ---------- tables ----------
def border(cell, edge, sz):
    tcPr = cell._tc.get_or_add_tcPr()
    b = tcPr.find(qn("w:tcBorders"))
    if b is None: b = OxmlElement("w:tcBorders"); tcPr.append(b)
    e = OxmlElement(f"w:{edge}"); e.set(qn("w:val"), "single"); e.set(qn("w:sz"), str(sz)); e.set(qn("w:color"), "000000")
    b.append(e)


def split_top(s, sep):
    out, depth, cur, i = [], 0, [], 0
    while i < len(s):
        if s[i] == "{": depth += 1
        elif s[i] == "}": depth -= 1
        if depth == 0 and s.startswith(sep, i) and (sep != "&" or s[i - 1:i] != "\\"):
            out.append("".join(cur)); cur = []; i += len(sep); continue
        cur.append(s[i]); i += 1
    out.append("".join(cur))
    return out


def parse_rows(block):
    """Returns (rows, rule_after) where rows are lists of (text, span) and rule_after marks rows followed by a rule."""
    rows, rules, cmid = [], {}, {}
    for raw in split_top(block, "\\\\"):
        raw = raw.strip()
        while True:
            m = re.match(r"\\(toprule|midrule|bottomrule)|\\cmidrule\(\w*\)\{(\d+)-(\d+)\}", raw)
            if not m: break
            if m.group(1) in ("midrule", "bottomrule") and rows: rules[len(rows) - 1] = 8 if m.group(1) == "bottomrule" else 4
            elif m.group(1) == "toprule": rules[-1] = 8
            elif m.group(2): cmid.setdefault(len(rows) - 1, []).append((int(m.group(2)), int(m.group(3))))
            raw = raw[m.end():].strip()
        if not raw: continue
        cells = []
        for c in split_top(raw, "&"):
            c = c.strip(); mm = re.match(r"\\multicolumn\{(\d+)\}\{\w\}\{(.*)\}$", c, re.S)
            cells.append((mm.group(2), int(mm.group(1)), "c") if mm else (c, 1, None))
        rows.append(cells)
    return rows, rules, cmid


def build_table(spec, rows, rules, cmid, header_rows, full_width=True, repeat_header=False):
    aligns = [{"Y": "l", "X": "l"}.get(ch, ch) for ch in spec if ch in "YXlcr"]
    flex = [ch in "YX" for ch in spec if ch in "YXlcr"]
    n = len(aligns)
    lens = [0] * n
    for r in rows:
        col = 0
        for text, span, _ in r:
            if span == 1: lens[col] = max(lens[col], len(re.sub(r"\\[a-z]+|[{}$]", "", text)))
            col += span
    fixed = [0 if flex[c] else min(3.2, 0.2 * lens[c] + 0.45) for c in range(n)]
    if full_width and any(flex):
        rest = (TEXT_W - sum(fixed)) / sum(flex)
        widths = [rest if flex[c] else fixed[c] for c in range(n)]
    else:
        widths = [max(1.2, 0.2 * lens[c] + 0.5) for c in range(n)]
    t = doc.add_table(rows=len(rows), cols=n)
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    for ri, r in enumerate(rows):
        col = 0
        for text, span, force in r:
            cell = t.cell(ri, col)
            if span > 1: cell = cell.merge(t.cell(ri, col + span - 1))
            p = cell.paragraphs[0]
            a = force or aligns[col]
            p.alignment = {"l": WD_ALIGN_PARAGRAPH.LEFT, "c": WD_ALIGN_PARAGRAPH.CENTER, "r": WD_ALIGN_PARAGRAPH.RIGHT}[a]
            p.paragraph_format.first_line_indent = Cm(0)
            p.paragraph_format.space_before = Pt(1); p.paragraph_format.space_after = Pt(1)
            inline(p, text, size=10)
            col += span
        for c in range(n):
            t.cell(ri, c).width = Cm(widths[c])
        if ri in rules:
            for c in range(n): border(t.cell(ri, c), "bottom", rules[ri])
        for a, b in cmid.get(ri, []):
            for c in range(a - 1, b): border(t.cell(ri, c), "bottom", 4)
        if ri == 0 and -1 in rules:
            for c in range(n): border(t.cell(0, c), "top", 8)
        if repeat_header and ri < header_rows:
            trPr = t.rows[ri]._tr.get_or_add_trPr(); h = OxmlElement("w:tblHeader"); h.set(qn("w:val"), "true"); trPr.append(h)
        if ri < header_rows:
            trPr = t.rows[ri]._tr.get_or_add_trPr(); cs = OxmlElement("w:cantSplit"); trPr.append(cs)
    for c, w in enumerate(widths):
        t.columns[c].width = Cm(w)
    return t


def table_env(block):
    cap = re.search(r"\\caption\{", block); text, _ = read_arg(block, cap.end() - 1)
    label = re.search(r"\\label\{([^}]*)\}", block).group(1)
    caption("Tabel", label, text, keep=True)
    m = re.search(r"\\begin\{tabularx\}\{[^}]*\}\{([^}]*)\}(.*?)\\end\{tabularx\}", block, re.S)
    rows, rules, cmid = parse_rows(m.group(2))
    header = max([k for k, v in rules.items() if v == 4 and k >= 0] or [0]) + 1
    build_table(m.group(1), rows, rules, cmid, header)
    note = block[m.end():]
    note = re.sub(r"\\end\{table\}", "", note)
    note = re.sub(r"\\vspace\{[^}]*\}|\\par|\\noindent|\\raggedright", "", note).strip()
    if note:
        p = para(note, align=WD_ALIGN_PARAGRAPH.LEFT, indent=False); p.paragraph_format.space_before = Pt(3)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)


def longtable_env(block):
    spec = re.match(r"\\begin\{longtable\}\{([^}]*)\}", block).group(1)
    cap = re.search(r"\\caption\{", block); text, _ = read_arg(block, cap.end() - 1)
    label = re.search(r"\\label\{([^}]*)\}", block).group(1)
    caption("Tabel", label, text, keep=True)
    head = block[block.index("\\\\", cap.end()) + 2:block.index(r"\endfirsthead")]
    rows_body = block[block.index(r"\endfoot") + len(r"\endfoot"):block.index(r"\end{longtable}")]
    hrows, hrules, _ = parse_rows(head)
    brows, _, _ = parse_rows(rows_body)
    rules = {-1: 8, len(hrows) - 1: 4, len(hrows) + len(brows) - 1: 8}
    build_table(spec, hrows + brows, rules, {}, len(hrows), full_width=False, repeat_header=True)
    doc.add_paragraph()


# ---------- figures ----------
tikz_count = [0]


def picture(par, path, width_cm):
    par.add_run().add_picture(path, width=Cm(width_cm))


def figure_env(block):
    label = re.search(r"\\label\{(fig:[^}]*)\}", block).group(1)
    main = re.sub(r"\\begin\{subfigure\}.*?\\end\{subfigure\}", "", block, flags=re.S)
    cap = list(re.finditer(r"\\caption\{", main))[-1]; text, _ = read_arg(main, cap.end() - 1)
    if "tikzpicture" in block:
        tikz_count[0] += 1
        from PIL import Image
        path = os.path.join(HERE, f"tikz{tikz_count[0]}.png")
        w = min(TEXT_W, Image.open(path).size[0] / 300 * 2.54)
        p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.keep_with_next = True
        p.paragraph_format.space_before = Pt(6); p.paragraph_format.first_line_indent = Cm(0)
        picture(p, path, w)
    else:
        letter = ord("a")
        for row in re.split(r"\\par\\medskip", block):
            subs = re.findall(r"\\begin\{subfigure\}\[t\]\{([0-9.]+)\\linewidth\}(.*?)\\end\{subfigure\}", row, re.S)
            if not subs: continue
            t = doc.add_table(rows=1, cols=len(subs)); t.alignment = WD_TABLE_ALIGNMENT.CENTER; t.autofit = False
            for ci, (frac, inner) in enumerate(subs):
                w = float(frac) * TEXT_W
                cell = t.cell(0, ci); cell.width = Cm(w)
                img = re.search(r"\\includegraphics(?:\[[^\]]*\])?\{([^}]*)\}", inner).group(1)
                sub_text, _ = read_arg(inner, re.search(r"\\caption\{", inner).end() - 1)
                p = cell.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.first_line_indent = Cm(0)
                picture(p, os.path.join(PAPER, "figures", img + ".jpg"), w - 0.2)
                q = cell.add_paragraph(); q.alignment = WD_ALIGN_PARAGRAPH.CENTER; q.paragraph_format.space_after = Pt(4)
                add_run(q, f"({chr(letter)}) ", {}, 9); inline(q, sub_text, size=9); letter += 1
            trPr = t.rows[0]._tr.get_or_add_trPr(); trPr.append(OxmlElement("w:cantSplit"))
    caption("Gambar", label, text)


# ---------- equation (OMML) ----------
M = "http://schemas.openxmlformats.org/officeDocument/2006/math"


def mr(text, italic=True, bold=False):
    sty = "bi" if (bold and italic) else "b" if bold else "i" if italic else "p"
    return (f'<m:r><m:rPr><m:sty m:val="{sty}"/></m:rPr><w:rPr><w:rFonts w:ascii="Cambria Math" w:hAnsi="Cambria Math"/></w:rPr>'
            f'<m:t xml:space="preserve">{text}</m:t></m:r>')


def msub(base, sub):
    return f"<m:sSub><m:e>{base}</m:e><m:sub>{sub}</m:sub></m:sSub>"


def equation(number):
    omml = (msub(mr("c"), mr("t")) + mr("(", False) + mr("u") + mr(",", False) + mr("v") + mr(")=6+", False) + mr("\u03bb")
            + mr("1", False, True) + mr("{", False) + mr("v") + mr("\u2208", False) + msub(mr("W"), mr("t")) + mr("},\u2003\u2003", False)
            + mr("v") + mr("\u2209", False) + msub(mr("B"), mr("t")) + mr(".", False))
    p = doc.add_paragraph(); p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.space_before = Pt(4); p.paragraph_format.space_after = Pt(4)
    ts = p.paragraph_format.tab_stops
    ts.add_tab_stop(Cm(TEXT_W / 2), WD_TAB_ALIGNMENT.CENTER); ts.add_tab_stop(Cm(TEXT_W), WD_TAB_ALIGNMENT.RIGHT)
    add_run(p, "\t", {}, 10)
    p._p.append(parse_xml(f'<m:oMath xmlns:m="{M}" {nsdecls("w")}>{omml}</m:oMath>'))
    add_run(p, f"\t({number})", {}, 10)


# ---------- title block ----------
def title_block(block):
    title = re.search(r"\\bfseries (.*?)\\par\}", block, re.S).group(1)
    p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_after = Pt(8)
    inline(p, title, {"bold": True}, size=16)
    rest = block[block.index(r"\par}") + len(r"\par}"):]
    rest = rest.replace(r"\end{center}", "")
    rest = rest[rest.index(r"\selectfont") + len(r"\selectfont"):].rstrip().rstrip("}")
    for chunk in rest.split(r"\par"):
        chunk = re.sub(r"\\vspace\{[^}]*\}", "", chunk).strip()
        if not chunk: continue
        q = doc.add_paragraph(); q.alignment = WD_ALIGN_PARAGRAPH.CENTER
        if chunk.startswith("\\textsuperscript{1}Teknik"): q.paragraph_format.space_before = Pt(4)
        inline(q, chunk, size=11)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)


# ---------- main walk ----------
BLOCKS = re.compile(r"\\begin\{(center|spacing|figure|table|longtable|equation|thebibliography)\}|\\(sub)?section\*\{|\\clearpage|\\FloatBarrier|\\setlength\{[^}]*\}\{[^}]*\}|\\vspace\{[^}]*\}")
pos, first_after_heading = 0, True


def text_chunk(chunk):
    global first_after_heading
    for part in re.split(r"\n\s*\n", chunk):
        part = part.strip()
        if not part: continue
        noindent = part.startswith(r"\noindent")
        para(part, indent=not (noindent or first_after_heading))
        first_after_heading = False


while True:
    m = BLOCKS.search(body, pos)
    text_chunk(body[pos:m.start()] if m else body[pos:])
    if not m: break
    tok = m.group(0)
    if tok.startswith(r"\section") or tok.startswith(r"\subsection"):
        title, end = read_arg(body, m.end() - 1)
        doc.add_paragraph(re.sub(r"\\emph\{([^}]*)\}", r"\1", title), style="Heading 1" if tok.startswith(r"\section") else "Heading 2")
        pos, first_after_heading = end, True
        continue
    if tok == r"\clearpage":
        doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE); pos = m.end(); continue
    if m.group(1) is None:
        pos = m.end(); continue
    env = m.group(1)
    end_tok = r"\end{" + env + "}"
    stop = body.index(end_tok, m.end()) + len(end_tok)
    block = body[m.start():stop]
    if env == "center": title_block(block)
    elif env == "spacing":
        inner = block[block.index("}{1.5}") + 6:block.rindex(r"\end{spacing}")]
        p = para(inner, indent=False, spacing=1.5)
    elif env == "figure": figure_env(block)
    elif env == "table": table_env(block)
    elif env == "longtable": longtable_env(block)
    elif env == "equation": equation(refs[re.search(r"\\label\{([^}]*)\}", block).group(1)])
    elif env == "thebibliography":
        refname = re.search(r"\\renewcommand\{\\refname\}\{([^}]*)\}", tex)
        doc.add_paragraph(refname.group(1) if refname else "Daftar Pustaka", style="Heading 1")
        for key, text in re.findall(r"\\bibitem\{([^}]*)\}(.*?)(?=\\bibitem|\\end\{thebibliography\})", block, re.S):
            p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
            pf = p.paragraph_format; pf.left_indent = Cm(0.8); pf.first_line_indent = Cm(-0.8); pf.space_after = Pt(2)
            pf.tab_stops.add_tab_stop(Cm(0.8))
            add_run(p, f"[{bib[key]}]\t", {}, 10); inline(p, text.strip(), size=10)
    pos = stop
    first_after_heading = env in ("center",)

props = doc.core_properties
props.title = "SAFE-MINING EVAC: Simulasi Navigasi Evakuasi Adaptif"
props.author = "Maulana Chandra Irawan; Khoirul Yardan Mauluddin Zhorif; Bagus Insan Pradana"
doc.save(OUT)
print("saved", OUT, "| figures", len([k for k in refs if k.startswith("fig:")]), "| tables", len(tables), "| refs", len(bib))
