from pathlib import Path
source=Path('D:/Rokas/reactiveturns-b2-staging/cinematic/render_authored_poses.py').read_text(encoding='utf-8')
exec(source.split("for view in ['front'")[0])
BASE=Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation')
for name,frames in [('Two handed sword block',[0,22,41,56,94]),('Two handed dodge backstep',[0,12,29,46,94]),('Normal preparation',[36]),('Heavy preparation',[56]),('Enter battle settle',[0,56])]:
 for frame in frames:render(name,frame,'threequarter')
