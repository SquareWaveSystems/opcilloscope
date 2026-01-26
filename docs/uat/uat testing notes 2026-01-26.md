- should hardcode 'opc.tcp://' in the connection dialog and handle it intelligently when pasted - actioned

- should auto populate address for connect after disconnect - actioned

- in the config file make this: 
192.168.1.6750000_202601261648.cfg
into this
192.168.1.67-50000_20260126_1648.cfg - actioned

- enter also needs to expand the node as well as subscribe. no key to expand the node? - actioned

-  b_kin  opcilloscope config.cfg
Terminal.Gui ConfigurationManager encountered the following errors while deserializing configuration files:
Error deserializing resource://[Terminal.Gui]/Terminal.Gui.Resources.config.json: Unknown property name "$schema
".

Terminal.Gui ConfigurationManager encountered the following errors while deserializing configuration files:
Error deserializing resource://[Terminal.Gui]/Terminal.Gui.Resources.config.json: Unknown property name "$schema
".

 b_kin  opcilloscope 192.168.1.6750000_202601261648.cfg
Terminal.Gui ConfigurationManager encountered the following errors while deserializing configuration files:
Error deserializing resource://[Terminal.Gui]/Terminal.Gui.Resources.config.json: Unknown property name "$schema
".

Terminal.Gui ConfigurationManager encountered the following errors while deserializing configuration files:
Error deserializing resource://[Terminal.Gui]/Terminal.Gui.Resources.config.json: Unknown property name "$schema
".

- b_kin  opcilloscope --help
Terminal.Gui ConfigurationManager encountered the following errors while deserializing configuration files:
Error deserializing resource://[Terminal.Gui]/Terminal.Gui.Resources.config.json: Unknown property name "$schema
".

-both s and t keys not working at the first statrup, but work after. need more investigation. using windows terminal to test. other hotkeys, like tab, space, enter, help, del are working

-there is a bug with left clicks in the monitored variables pane. It is selecting/deselecting seemingly random variables. Instad it should only tick the Sel box when clicked there, otherwise only select the line. e.g. click to select the line, spacxe to scelect for recording. - actioned

-  M opens file menu, but F is suffcient it - actioned

-can we get mousewheel working in scroll view?

-can we do horiz and vert scale in scope view?

- once recording is triggered by ctrl+r, how is it stopped? should be easy

- whay are we reording to C:\Users\USER\OneDrive\Documents\opcilloscope\recordings
    - is that just the standard documents folder, if so, ok

- vsc data seems to be screwed up (have file)

## other notes
-mouse control working well over all
-seems responsive
-make the scope more usable, add to roadmap
