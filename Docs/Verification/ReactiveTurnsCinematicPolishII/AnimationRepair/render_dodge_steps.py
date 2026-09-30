from pathlib import Path
exec(Path('D:/Rokas/reactiveturns-b2-staging/cinematic/render_authored_poses.py').read_text(encoding='utf-8').split("for view in ['front'")[0])
BASE=Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation')
for frame in [46,60,72,84,96]:render('Two handed dodge backstep',frame,'threequarter')
