FNR==1 { if (file) flush(); file=FILENAME; summ=""; insum=0; types=""; done=0 }
/<summary>/ && !done { insum=1 }
insum { line=$0; gsub(/^[ \t]*\/\/\/[ \t]?/,"",line); gsub(/<\/?summary>/,"",line); gsub(/<see cref="([^"]*)"\/>/,"&",line); summ=summ " " line; if ($0 ~ /<\/summary>/) { insum=0; done=1 } }
/^(public|internal)[ a-z]*(class|record|interface|enum|struct) / { t=$0; sub(/^(public|internal)[ a-z]*(class|record|interface|enum|struct) /,"",t); sub(/[ (:<{].*/,"",t); if (index(types, t)==0) types=types (types?", ":"") t }
function flush() { s=summ; gsub(/<see cref="[A-Za-z]:?/,"`",s); gsub(/"\/>/,"`",s); gsub(/<[^>]*>/,"",s); gsub(/[ \t]+/," ",s); sub(/^ /,"",s); if (length(s)>200) s=substr(s,1,197) "..."; f=file; sub(/^src\/[^\/]*\//,"",f); print "- `" f "` — " (types?"**" types "** — ":"") s }
END { flush() }
