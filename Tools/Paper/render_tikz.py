import re, subprocess, os

BUILD = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Documentation", "Paper", "build")
os.makedirs(BUILD, exist_ok=True)
os.chdir(BUILD)
tex = open(os.path.join(BUILD, "..", "document.tex"), encoding="utf-8").read()
pics = re.findall(r"\\begin\{tikzpicture\}.*?\\end\{tikzpicture\}", tex, re.S)
pre = (r"\documentclass[border=6pt]{standalone}" "\n"
       r"\usepackage[T1]{fontenc}\usepackage[utf8]{inputenc}\usepackage{mathptmx}" "\n"
       r"\usepackage{tikz}\usetikzlibrary{arrows.meta}" "\n"
       r"\begin{document}" "\n")
for i, p in enumerate(pics, 1):
    name = f"tikz{i}"
    open(name + ".tex", "w", encoding="utf-8").write(pre + p + "\n" + r"\end{document}" + "\n")
    subprocess.run(["pdflatex", "--enable-installer", "-interaction=nonstopmode", name + ".tex"], capture_output=True)
    subprocess.run(["pdftoppm", "-r", "300", "-png", "-singlefile", name + ".pdf", name], check=True)
    print(name, "ok")
