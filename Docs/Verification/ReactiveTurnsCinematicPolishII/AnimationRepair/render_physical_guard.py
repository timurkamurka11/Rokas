from pathlib import Path
source=Path('D:/Rokas/reactiveturns-b2-staging/cinematic/render_authored_poses.py').read_text(encoding='utf-8')
exec(source.split("for view in ['front'")[0])
BASE=Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation')
for frame in [0,22,41,56,94]:render('Two handed sword block',frame,'threequarter')
for view in ['front','side']:render('Two handed sword block',41,view)
